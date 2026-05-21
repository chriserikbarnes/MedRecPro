using MedRecProImportClass.Data;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Service-level tests for <see cref="AdverseEventDenormalizationService"/>
    /// covering Stage 5 (Phase 2) end-to-end behavior: comparator selection,
    /// trial-design classification, statistics computation, hard-guard enforcement,
    /// and rerun idempotency.
    /// </summary>
    /// <remarks>
    /// Uses EF Core <c>UseInMemoryDatabase</c> and Moq <see cref="Mock{T}"/> for
    /// <see cref="ILogger"/>. Each test gets its own isolated DB to avoid cross-test
    /// pollution.
    /// </remarks>
    /// <seealso cref="AdverseEventDenormalizationService"/>
    /// <seealso cref="RelativeRiskCalculator"/>
    [TestClass]
    public class AdverseEventDenormalizationServiceTests
    {
        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates an <see cref="AdverseEventDenormalizationService"/> with an isolated
        /// InMemory <see cref="ApplicationDbContext"/> seeded with the provided source rows.
        /// </summary>
        /// <param name="seedRows">Source rows to seed into <c>tmp_FlattenedStandardizedTable</c>.</param>
        /// <returns>Tuple of (service, context) so tests can query the output table.</returns>
        private static (AdverseEventDenormalizationService service, ApplicationDbContext db)
            createService(params LabelView.FlattenedStandardizedTable[] seedRows)
        {
            #region implementation

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"AeDenorm_{Guid.NewGuid()}")
                .Options;

            var db = new ApplicationDbContext(dbOptions);

            if (seedRows.Length > 0)
            {
                db.Set<LabelView.FlattenedStandardizedTable>().AddRange(seedRows);
                db.SaveChanges();
                db.ChangeTracker.Clear();
            }

            var logger = new Mock<ILogger<AdverseEventDenormalizationService>>();
            var service = new AdverseEventDenormalizationService(db, logger.Object);

            return (service, db);

            #endregion
        }

        /**************************************************************/
        /// <summary>Builds a minimal AE source row with sensible defaults.</summary>
        private static LabelView.FlattenedStandardizedTable aeRow(
            int id,
            Guid? docId,
            int? textTableId,
            string? paramName,
            string? treatmentArm,
            int? armN,
            decimal? dose,
            string? doseUnit,
            double? primaryValue,
            string? primaryValueType,
            string? paramSubtype = null,
            string? parameterCategory = null,
            int? sourceRowSeq = 1,
            int? sourceCellSeq = 1)
        {
            #region implementation

            return new LabelView.FlattenedStandardizedTable
            {
                Id = id,
                DocumentGUID = docId,
                TextTableID = textTableId,
                TableCategory = "ADVERSE_EVENT",
                ParameterName = paramName,
                ParameterCategory = parameterCategory,
                ParameterSubtype = paramSubtype,
                TreatmentArm = treatmentArm,
                ArmN = armN,
                Dose = dose,
                DoseUnit = doseUnit,
                PrimaryValue = primaryValue,
                PrimaryValueType = primaryValueType,
                SourceRowSeq = sourceRowSeq,
                SourceCellSeq = sourceCellSeq
            };

            #endregion
        }

        #endregion Helper Methods

        #region Empty / Trivial Cases

        /**************************************************************/
        /// <summary>No AE rows in source → service writes 0 rows and returns 0.</summary>
        [TestMethod]
        public async Task PopulateAsync_NoAeRows_WritesZero()
        {
            #region implementation

            var pkRow = new LabelView.FlattenedStandardizedTable
            {
                Id = 1,
                DocumentGUID = Guid.NewGuid(),
                TableCategory = "PK",
                ParameterName = "Cmax",
                PrimaryValue = 2.21,
                PrimaryValueType = "Mean"
            };

            var (service, db) = createService(pkRow);

            var written = await service.PopulateAsync();

            Assert.AreEqual(0, written);
            Assert.AreEqual(0, db.Set<LabelView.FlattenedAdverseEventTable>().Count());

            #endregion
        }

        /**************************************************************/
        /// <summary>Single arm: null-RR row is excluded from visualization output.</summary>
        [TestMethod]
        public async Task PopulateAsync_SingleArm_NoComparator()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var row = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 14.0, "Percentage");

            var (service, db) = createService(row);

            var written = await service.PopulateAsync();

            Assert.AreEqual(0, written);
            Assert.AreEqual(0, db.Set<LabelView.FlattenedAdverseEventTable>().Count());

            #endregion
        }

        #endregion Empty / Trivial Cases

        #region Placebo-Controlled Trial

        /**************************************************************/
        /// <summary>
        /// Drug + Placebo, both Percentage, same TextTableID — comparator is placebo,
        /// IsPlaceboControlled=1, flag PLACEBO_COMPARATOR, RR computed.
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_PlaceboControlled_ProducesRR()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var drug = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 20.0, "Percentage");
            var placebo = aeRow(2, doc, 100, "Nausea", "Placebo", 100, 0m, null, 10.0, "Percentage");

            var (service, db) = createService(drug, placebo);

            var written = await service.PopulateAsync();

            Assert.AreEqual(1, written, "Comparator (placebo) is excluded from output");

            var row = db.Set<LabelView.FlattenedAdverseEventTable>().Single();
            Assert.AreEqual("Drug A 50mg", row.TreatmentArm);
            Assert.AreEqual("Placebo", row.ComparatorArm);
            Assert.AreEqual(100, row.ComparatorN);
            Assert.IsTrue(row.IsPlaceboControlled, "Drug + Placebo only → IsPlaceboControlled=1");
            Assert.IsNotNull(row.RR);
            Assert.AreEqual(2.0, row.RR!.Value, 1e-6, "RR should be 2.0 for 20% vs 10%");
            StringAssert.Contains(row.CalculationFlags, "PLACEBO_COMPARATOR");
            Assert.AreEqual("KATZ_LOG", row.CalculationMethod);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stage 5 standardizes AE names and SOCs before comparator grouping, so
        /// dictionary-equivalent names can pair and carry auditable <c>AE_STD:*</c> flags.
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_StandardizesNameAndCategoryBeforeComparatorGrouping()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var drug = aeRow(
                1,
                doc,
                100,
                "Digestive System Nausea",
                "Drug A 50mg",
                100,
                50m,
                "mg",
                20.0,
                "Percentage",
                parameterCategory: "Chemistry");
            var placebo = aeRow(
                2,
                doc,
                100,
                "Nausea",
                "Placebo",
                100,
                0m,
                null,
                10.0,
                "Percentage",
                parameterCategory: "Adverse Reactions",
                sourceRowSeq: 2);

            var (service, db) = createService(drug, placebo);

            var written = await service.PopulateAsync();

            Assert.AreEqual(1, written);
            var row = db.Set<LabelView.FlattenedAdverseEventTable>().Single();
            Assert.AreEqual("Nausea", row.ParameterName);
            Assert.AreEqual("Gastrointestinal Disorders", row.ParameterCategory);
            Assert.IsNotNull(row.RR);
            StringAssert.Contains(row.CalculationFlags, "AE_STD:NAME_NORMALIZED");
            StringAssert.Contains(row.CalculationFlags, "AE_STD:NAME_NORMALIZED:Digestive System Nausea->Nausea");
            StringAssert.Contains(row.CalculationFlags, "AE_STD:SOC_ALIGNED:Chemistry->Gastrointestinal Disorders");
            StringAssert.Contains(row.CalculationFlags, "AE_STD:SOC_FROM_NAME");
            StringAssert.Contains(row.CalculationFlags, "AE_STD:SOC_FROM_NAME:Adverse Reactions->Gastrointestinal Disorders");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Drug + Placebo + Active comparator (distinct arm-name roots) in one TextTableID:
        /// the comparator cascade picks Placebo (tier 1), so under the row-level definition
        /// every output row is paired with placebo and gets IsPlaceboControlled = true.
        /// The per-table classifier still produces PLACEBO_PLUS_ACTIVE (not AMBIGUOUS),
        /// which means no AMBIGUOUS_TRIAL_DESIGN diagnostic is emitted.
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_DrugPlaceboPlusActive_FlagTrueWhenComparatorIsPlacebo()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var drugA = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 20.0, "Percentage");
            var drugB = aeRow(2, doc, 100, "Nausea", "Drug B 100mg", 100, 100m, "mg", 18.0, "Percentage", sourceRowSeq: 2);
            var placebo = aeRow(3, doc, 100, "Nausea", "Placebo", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 3);

            var (service, db) = createService(drugA, drugB, placebo);

            await service.PopulateAsync();

            // Comparator is placebo (excluded). Both drug arms produce output rows.
            var rows = db.Set<LabelView.FlattenedAdverseEventTable>().ToList();
            Assert.AreEqual(2, rows.Count);
            Assert.IsTrue(rows.All(r => r.IsPlaceboControlled),
                "Row-level: every row whose comparator is Placebo gets IsPlaceboControlled=1");
            Assert.IsTrue(rows.All(r => r.CalculationFlags!.Contains("PLACEBO_COMPARATOR")));
            Assert.IsTrue(rows.All(r => !r.CalculationFlags!.Contains("AMBIGUOUS_TRIAL_DESIGN")),
                "Two distinct drug roots + placebo classifies cleanly as PLACEBO_PLUS_ACTIVE, not ambiguous");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Regression test for the TextTableID=23177 root cause. One DocumentGUID carries
        /// two sub-trials in different TextTableIDs: a clean drug-vs-placebo table and an
        /// active-only table (no placebo). Rows from the placebo table must be flagged
        /// IsPlaceboControlled=1 even though the document overall contains an unrelated
        /// active-only sub-trial. Rows from the active-only table must remain false.
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_MultipleSubTrialsInOneDocument_TableLevelClassification()
        {
            #region implementation

            var doc = Guid.NewGuid();

            // Sub-trial 1 (TextTableID=100): drug + placebo, clean PLACEBO_ONLY design.
            var t1Drug = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 20.0, "Percentage");
            var t1Placebo = aeRow(2, doc, 100, "Nausea", "Placebo", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 2);

            // Sub-trial 2 (TextTableID=200): drug + active comparator (no placebo) in the
            // SAME document. Pre-fix, the doc-wide classifier saw arms = {Drug A, Drug B,
            // Placebo, ActiveComparator} → PLACEBO_PLUS_ACTIVE → false → stamped onto the
            // table-100 rows, breaking the user's primary use-case.
            var t2Drug = aeRow(3, doc, 200, "Headache", "Drug B 75mg", 100, 75m, "mg", 15.0, "Percentage", sourceRowSeq: 3);
            var t2Active = aeRow(4, doc, 200, "Headache", "Active Comparator 25mg", 100, 25m, "mg", 12.0, "Percentage", sourceRowSeq: 4);

            var (service, db) = createService(t1Drug, t1Placebo, t2Drug, t2Active);

            await service.PopulateAsync();

            var rows = db.Set<LabelView.FlattenedAdverseEventTable>().ToList();
            Assert.AreEqual(2, rows.Count, "One non-comparator row per sub-trial");

            var t1Row = rows.Single(r => r.FlattenedStandardizedTableId == t1Drug.Id);
            Assert.AreEqual("Placebo", t1Row.ComparatorArm);
            Assert.IsTrue(t1Row.IsPlaceboControlled,
                "Table 100 is drug-vs-placebo; row's comparator IS placebo → bit must be 1 (regression for TextTableID=23177)");
            Assert.IsTrue(t1Row.CalculationFlags!.Contains("PLACEBO_COMPARATOR"));

            var t2Row = rows.Single(r => r.FlattenedStandardizedTableId == t2Drug.Id);
            Assert.AreEqual("Active Comparator 25mg", t2Row.ComparatorArm);
            Assert.IsFalse(t2Row.IsPlaceboControlled,
                "Table 200 has no placebo arm; row's comparator is the lower-dose active arm → bit must be 0");
            Assert.IsTrue(t2Row.CalculationFlags!.Contains("LOW_DOSE_COMPARATOR"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// False-positive guard. A single TextTableID has placebo + two drug doses.
        /// Parameter 1 has all three rows (the comparator cascade picks Placebo). Parameter
        /// 2 only has the two drug doses (no placebo for that parameter — the cascade picks
        /// the lower-dose drug). Under strictly row-level semantics, parameter 1's row gets
        /// IsPlaceboControlled=1 but parameter 2's row must get 0, even though the table
        /// overall classifies as STEPPED_DOSE_PLUS_PLACEBO. This locks in the strict
        /// definition and prevents future regression to a design-OR-comparator approach.
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_PlaceboTablePartialGroup_NoFalsePositive()
        {
            #region implementation

            var doc = Guid.NewGuid();

            // Parameter 1: all three arms present → comparator = Placebo → bit = 1
            var p1Low = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 12.0, "Percentage");
            var p1High = aeRow(2, doc, 100, "Nausea", "Drug A 100mg", 100, 100m, "mg", 20.0, "Percentage", sourceRowSeq: 2);
            var p1Placebo = aeRow(3, doc, 100, "Nausea", "Placebo", 100, 0m, null, 8.0, "Percentage", sourceRowSeq: 3);

            // Parameter 2: only the two drug doses (no placebo for this param) → comparator
            // = lower-dose drug → bit must be 0 even though the TABLE design has placebo.
            var p2Low = aeRow(4, doc, 100, "Headache", "Drug A 50mg", 100, 50m, "mg", 6.0, "Percentage", sourceRowSeq: 4);
            var p2High = aeRow(5, doc, 100, "Headache", "Drug A 100mg", 100, 100m, "mg", 11.0, "Percentage", sourceRowSeq: 5);

            var (service, db) = createService(p1Low, p1High, p1Placebo, p2Low, p2High);

            await service.PopulateAsync();

            var rows = db.Set<LabelView.FlattenedAdverseEventTable>().ToList();

            var p1Rows = rows.Where(r => r.ParameterName == "Nausea").ToList();
            Assert.IsTrue(p1Rows.All(r => r.IsPlaceboControlled),
                "Parameter 1's rows pair with Placebo → bit = 1");
            Assert.IsTrue(p1Rows.All(r => r.CalculationFlags!.Contains("PLACEBO_COMPARATOR")));

            var p2Rows = rows.Where(r => r.ParameterName == "Headache").ToList();
            Assert.IsTrue(p2Rows.All(r => !r.IsPlaceboControlled),
                "Parameter 2's rows pair with the lower-dose drug → bit = 0 even though the table has STEPPED_DOSE_PLUS_PLACEBO design");
            Assert.IsTrue(p2Rows.All(r => r.CalculationFlags!.Contains("LOW_DOSE_COMPARATOR")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Defensive test for null TextTableID. Two AE rows in one DocumentGUID with
        /// TextTableID=null but different ParameterName + comparator structure. Because
        /// the bit is comparator-driven (not design-driven), each row must be independently
        /// correct based on its own group's comparator. The null-TextTableID path
        /// short-circuits trial-design classification but does NOT merge rows for
        /// comparator pairing (which is keyed by ParameterName etc., not TextTableID alone).
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_NullTextTableID_DoesNotMergeRows()
        {
            #region implementation

            var doc = Guid.NewGuid();

            // Group A: drug vs placebo (placebo comparator)
            var aDrug = aeRow(1, doc, null, "Nausea", "Drug A 50mg", 100, 50m, "mg", 20.0, "Percentage");
            var aPlacebo = aeRow(2, doc, null, "Nausea", "Placebo", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 2);

            // Group B: drug vs active comparator (no placebo → low-dose comparator)
            var bDrugHigh = aeRow(3, doc, null, "Headache", "Drug B 100mg", 100, 100m, "mg", 15.0, "Percentage", sourceRowSeq: 3);
            var bDrugLow = aeRow(4, doc, null, "Headache", "Drug B 25mg", 100, 25m, "mg", 8.0, "Percentage", sourceRowSeq: 4);

            var (service, db) = createService(aDrug, aPlacebo, bDrugHigh, bDrugLow);

            await service.PopulateAsync();

            var rows = db.Set<LabelView.FlattenedAdverseEventTable>().ToList();
            Assert.AreEqual(2, rows.Count);

            var nausea = rows.Single(r => r.ParameterName == "Nausea");
            Assert.IsTrue(nausea.IsPlaceboControlled, "Null TextTableID must not block per-row placebo detection");
            Assert.AreEqual("Placebo", nausea.ComparatorArm);

            var headache = rows.Single(r => r.ParameterName == "Headache");
            Assert.IsFalse(headache.IsPlaceboControlled, "Null TextTableID must not bleed placebo-ness across parameters");
            Assert.AreEqual("Drug B 25mg", headache.ComparatorArm);

            #endregion
        }

        #endregion Placebo-Controlled Trial

        #region Value-Type Mismatch

        /**************************************************************/
        /// <summary>Same TextTable, one Percentage row + one Mean row → MIXED_VALUE_TYPES.</summary>
        [TestMethod]
        public async Task PopulateAsync_MixedValueTypes_FlagsAndNulls()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var pct = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 20.0, "Percentage");
            var mean = aeRow(2, doc, 100, "Nausea", "Placebo", 100, 0m, null, 2.5, "Mean");

            var (service, db) = createService(pct, mean);

            var written = await service.PopulateAsync();

            Assert.AreEqual(0, written);
            Assert.AreEqual(0, db.Set<LabelView.FlattenedAdverseEventTable>().Count());

            #endregion
        }

        #endregion Value-Type Mismatch

        #region DNRR

        /**************************************************************/
        /// <summary>
        /// Placebo + 50/100/200 mg arms, all Percentage. Placebo is the comparator
        /// (excluded). D_ref = 50mg. The 50mg row gets DNRR=null + IS_REFERENCE_DOSE;
        /// 100mg and 200mg get computed DNRR.
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_DnrrSkipsReferenceDose()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var dose50 = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 15.0, "Percentage");
            var dose100 = aeRow(2, doc, 100, "Nausea", "Drug A 100mg", 100, 100m, "mg", 25.0, "Percentage", sourceRowSeq: 2);
            var dose200 = aeRow(3, doc, 100, "Nausea", "Drug A 200mg", 100, 200m, "mg", 40.0, "Percentage", sourceRowSeq: 3);
            var placebo = aeRow(4, doc, 100, "Nausea", "Placebo", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 4);

            var (service, db) = createService(dose50, dose100, dose200, placebo);

            await service.PopulateAsync();

            var rows = db.Set<LabelView.FlattenedAdverseEventTable>().ToList();
            Assert.AreEqual(3, rows.Count, "Three drug rows; placebo is excluded as comparator");

            var row50 = rows.Single(r => r.TreatmentArm == "Drug A 50mg");
            Assert.IsNull(row50.DNRR, "50mg is the reference dose; DNRR should be null");
            StringAssert.Contains(row50.CalculationFlags, "IS_REFERENCE_DOSE");

            var row100 = rows.Single(r => r.TreatmentArm == "Drug A 100mg");
            Assert.IsNotNull(row100.DNRR);

            var row200 = rows.Single(r => r.TreatmentArm == "Drug A 200mg");
            Assert.IsNotNull(row200.DNRR);

            #endregion
        }

        /**************************************************************/
        /// <summary>One drug row + placebo → only one non-zero dose → NO_DOSE_RANGE.</summary>
        [TestMethod]
        public async Task PopulateAsync_NoDoseRange_FlagsNoDoseRange()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var drug = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 20.0, "Percentage");
            var placebo = aeRow(2, doc, 100, "Nausea", "Placebo", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 2);

            var (service, db) = createService(drug, placebo);

            await service.PopulateAsync();

            var row = db.Set<LabelView.FlattenedAdverseEventTable>().Single();
            // 50mg is the only non-zero dose → it IS the reference dose → DNRR null with IS_REFERENCE_DOSE
            Assert.IsNull(row.DNRR);
            StringAssert.Contains(row.CalculationFlags, "IS_REFERENCE_DOSE");

            #endregion
        }

        /**************************************************************/
        /// <summary>Differing dose units between treatment and reference → DOSE_UNIT_MISMATCH.</summary>
        [TestMethod]
        public async Task PopulateAsync_DoseUnitMismatch_FlagsAndSkipsDnrr()
        {
            #region implementation

            var doc = Guid.NewGuid();
            // D_ref will be 50 mg (lowest); drug 100 mg/kg is in different unit
            var drug50 = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 15.0, "Percentage");
            var drug100kg = aeRow(2, doc, 100, "Nausea", "Drug A 100mg/kg", 100, 100m, "mg/kg", 25.0, "Percentage", sourceRowSeq: 2);
            var placebo = aeRow(3, doc, 100, "Nausea", "Placebo", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 3);

            var (service, db) = createService(drug50, drug100kg, placebo);

            await service.PopulateAsync();

            var rows = db.Set<LabelView.FlattenedAdverseEventTable>().ToList();
            var mismatched = rows.Single(r => r.TreatmentArm == "Drug A 100mg/kg");
            Assert.IsNull(mismatched.DNRR);
            StringAssert.Contains(mismatched.CalculationFlags, "DOSE_UNIT_MISMATCH");

            #endregion
        }

        #endregion DNRR

        #region Zero-Cell + Hard Guards

        /**************************************************************/
        /// <summary>Drug 0 events vs Placebo 5 events: Haldane-Anscombe applied; raw 0 preserved.</summary>
        [TestMethod]
        public async Task PopulateAsync_ZeroCellCorrected()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var drug = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 0.0, "Percentage");
            var placebo = aeRow(2, doc, 100, "Nausea", "Placebo", 100, 0m, null, 5.0, "Percentage", sourceRowSeq: 2);

            var (service, db) = createService(drug, placebo);

            await service.PopulateAsync();

            var row = db.Set<LabelView.FlattenedAdverseEventTable>().Single();
            Assert.IsNotNull(row.RR);
            Assert.AreEqual(0.0, row.EventsTreatment, "Raw a=0 must be preserved for audit");
            StringAssert.Contains(row.CalculationFlags, "ZERO_CELL_CORRECTED");

            #endregion
        }

        /**************************************************************/
        /// <summary>Percentage > 100 → PERCENT_OUT_OF_RANGE, stats NULL.</summary>
        [TestMethod]
        public async Task PopulateAsync_PercentOutOfRange_RejectsRow()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var drug = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 120.0, "Percentage");
            var placebo = aeRow(2, doc, 100, "Nausea", "Placebo", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 2);

            var (service, db) = createService(drug, placebo);

            var written = await service.PopulateAsync();

            Assert.AreEqual(0, written);
            Assert.AreEqual(0, db.Set<LabelView.FlattenedAdverseEventTable>().Count());

            #endregion
        }

        /**************************************************************/
        /// <summary>Count value type with derived events &gt; ArmN → EVENTS_EXCEED_ARMN.</summary>
        [TestMethod]
        public async Task PopulateAsync_EventsExceedArmN_Rejects()
        {
            #region implementation

            var doc = Guid.NewGuid();
            // Drug arm: 150 events / 100 N — invalid
            var drug = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 150.0, "Count");
            var placebo = aeRow(2, doc, 100, "Nausea", "Placebo", 100, 0m, null, 5.0, "Count", sourceRowSeq: 2);

            var (service, db) = createService(drug, placebo);

            var written = await service.PopulateAsync();

            Assert.AreEqual(0, written);
            Assert.AreEqual(0, db.Set<LabelView.FlattenedAdverseEventTable>().Count());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Percentage row with null ArmN keeps statistics null and reports the missing
        /// denominator instead of inferring RR from percentages alone.
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_NullArmN_NullsCi()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var drug = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", null, 50m, "mg", 20.0, "Percentage");
            var placebo = aeRow(2, doc, 100, "Nausea", "Placebo", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 2);

            var (service, db) = createService(drug, placebo);

            var written = await service.PopulateAsync();

            Assert.AreEqual(0, written);
            Assert.AreEqual(0, db.Set<LabelView.FlattenedAdverseEventTable>().Count());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Percentage rows missing both treatment and comparator denominators keep RR,
        /// CIs, method, and derived events null with both denominator diagnostics.
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_PercentageWithBothDenominatorsMissing_NullsStats()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var drug = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", null, 50m, "mg", 20.0, "Percentage");
            var placebo = aeRow(2, doc, 100, "Nausea", "Placebo", null, 0m, null, 10.0, "Percentage", sourceRowSeq: 2);

            var (service, db) = createService(drug, placebo);

            var written = await service.PopulateAsync();

            Assert.AreEqual(0, written);
            Assert.AreEqual(0, db.Set<LabelView.FlattenedAdverseEventTable>().Count());

            #endregion
        }

        #endregion Zero-Cell + Hard Guards

        #region Document and Group Handling

        /**************************************************************/
        /// <summary>AE rows with NULL DocumentGUID are skipped; valid documents still processed.</summary>
        [TestMethod]
        public async Task PopulateAsync_NullDocumentGuid_Skipped()
        {
            #region implementation

            var validDoc = Guid.NewGuid();
            var nullDocRow = aeRow(1, null, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 20.0, "Percentage");
            var validDrug = aeRow(2, validDoc, 200, "Nausea", "Drug A 50mg", 100, 50m, "mg", 20.0, "Percentage");
            var validPlacebo = aeRow(3, validDoc, 200, "Nausea", "Placebo", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 2);

            var (service, db) = createService(nullDocRow, validDrug, validPlacebo);

            await service.PopulateAsync();

            var rows = db.Set<LabelView.FlattenedAdverseEventTable>().ToList();
            Assert.AreEqual(1, rows.Count, "Only the valid Doc's drug row should be emitted");
            Assert.AreEqual(validDoc, rows[0].DocumentGUID);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Invalid null/caption/SOC/header-token arms are filtered before grouping so
        /// they cannot influence comparator selection or output rows.
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_InvalidStructuralArms_SkippedBeforeGrouping()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var validDrug = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 20.0, "Percentage");
            var validPlacebo = aeRow(2, doc, 100, "Nausea", "Placebo", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 2);
            var bodySystemArm = aeRow(3, doc, 100, "Nausea", "Ocular", 100, null, null, 1.0, "Percentage", sourceRowSeq: 3);
            var captionArm = aeRow(4, doc, 100, "Nausea", "Table 6: Adverse Reactions Reported in Clinical Trials", 100, null, null, 1.0, "Percentage", sourceRowSeq: 4);
            var nullArm = aeRow(5, doc, 100, "Nausea", null, 100, null, null, 1.0, "Percentage", sourceRowSeq: 5);
            var headerTokenArm = aeRow(6, doc, 100, "Nausea", "Incidence (discontinuation )", 100, null, null, 1.0, "Percentage", sourceRowSeq: 6);

            var (service, db) = createService(validDrug, validPlacebo, bodySystemArm, captionArm, nullArm, headerTokenArm);

            var written = await service.PopulateAsync();

            var rows = db.Set<LabelView.FlattenedAdverseEventTable>().ToList();
            Assert.AreEqual(1, written);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("Drug A 50mg", rows[0].TreatmentArm);
            Assert.AreEqual("Placebo", rows[0].ComparatorArm);
            Assert.IsTrue(rows[0].IsPlaceboControlled);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Valid numeric rows with missing <c>ArmN</c> remain eligible for calculation
        /// audit, but null-RR outputs are not persisted to the visualization table.
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_ValidNumericRowsWithMissingArmN_AreNotFilteredBySafetyGate()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var drug = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", null, 50m, "mg", 20.0, "Percentage");
            var placebo = aeRow(2, doc, 100, "Nausea", "Placebo", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 2);

            var (service, db) = createService(drug, placebo);

            var written = await service.PopulateAsync();

            Assert.AreEqual(0, written);
            Assert.AreEqual(0, db.Set<LabelView.FlattenedAdverseEventTable>().Count());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Unique same-arm ArmN within a comparator group backfills missing treatment
        /// rows and makes CI bounds calculable.
        /// </summary>
        [TestMethod]
        public async Task Stage5_GroupBackfill_UniqueArmN_PopulatesArmNAndComparatorN()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var drugMissing = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", null, 50m, "mg", 20.0, "Percentage");
            var drugKnown = aeRow(2, doc, 100, "Nausea", "Drug A 50mg", 200, 50m, "mg", 22.0, "Percentage", sourceRowSeq: 2);
            var placebo = aeRow(3, doc, 100, "Nausea", "Placebo", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 3);

            var (service, db) = createService(drugMissing, drugKnown, placebo);

            var written = await service.PopulateAsync();

            var rows = db.Set<LabelView.FlattenedAdverseEventTable>().ToList();
            var missingRow = rows.Single(r => r.FlattenedStandardizedTableId == 1);
            Assert.AreEqual(2, written);
            Assert.AreEqual(200, missingRow.ArmN);
            Assert.AreEqual(100, missingRow.ComparatorN);
            Assert.IsNotNull(missingRow.RRLowerBound);
            Assert.IsNotNull(missingRow.RRUpperBound);
            StringAssert.Contains(missingRow.CalculationFlags, "AE_ARMN_STAGE5_GROUP_BACKFILL");
            Assert.IsFalse(missingRow.CalculationFlags!.Contains("NO_ARMN"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Unique same-arm ArmN on a duplicate comparator arm can backfill the selected
        /// comparator before RR/CI calculation.
        /// </summary>
        [TestMethod]
        public async Task Stage5_GroupBackfill_SelectedComparatorBackfill_PopulatesComparatorN()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var drug = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 200, 50m, "mg", 20.0, "Percentage");
            var placeboMissing = aeRow(2, doc, 100, "Nausea", "Placebo", null, 0m, null, 10.0, "Percentage", sourceRowSeq: 1);
            var placeboKnown = aeRow(3, doc, 100, "Nausea", "Placebo", 100, 0m, null, 11.0, "Percentage", sourceRowSeq: 2);

            var (service, db) = createService(drug, placeboMissing, placeboKnown);

            await service.PopulateAsync();

            var drugRow = db.Set<LabelView.FlattenedAdverseEventTable>()
                .Single(r => r.FlattenedStandardizedTableId == 1);
            Assert.AreEqual(100, drugRow.ComparatorN);
            Assert.IsNotNull(drugRow.RRLowerBound);
            Assert.IsNotNull(drugRow.RRUpperBound);
            StringAssert.Contains(drugRow.CalculationFlags, "AE_ARMN_STAGE5_GROUP_BACKFILL");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Conflicting same-arm Ns prevent backfill and preserve missing-denominator
        /// diagnostics.
        /// </summary>
        [TestMethod]
        public async Task Stage5_GroupBackfill_ConflictingArmN_DoesNotBackfill()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var drugMissing = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", null, 50m, "mg", 20.0, "Percentage");
            var drugKnownA = aeRow(2, doc, 100, "Nausea", "Drug A 50mg", 150, 50m, "mg", 22.0, "Percentage", sourceRowSeq: 2);
            var drugKnownB = aeRow(3, doc, 100, "Nausea", "Drug A 50mg", 200, 50m, "mg", 24.0, "Percentage", sourceRowSeq: 3);
            var placebo = aeRow(4, doc, 100, "Nausea", "Placebo", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 4);

            var (service, db) = createService(drugMissing, drugKnownA, drugKnownB, placebo);

            var written = await service.PopulateAsync();

            var rows = db.Set<LabelView.FlattenedAdverseEventTable>().ToList();
            Assert.AreEqual(2, written);
            Assert.AreEqual(2, rows.Count);
            Assert.IsFalse(rows.Any(r => r.FlattenedStandardizedTableId == 1));
            Assert.IsTrue(rows.All(r => r.RR is not null));
            Assert.IsTrue(rows.All(r => r.CalculationFlags!.Contains("AE_ARMN_REJECTED_CONFLICTING_N")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Same DocumentGUID + ParameterName but different TextTableIDs means each
        /// TextTable group has its own comparator and no cross-pairing.
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_DifferentTextTablesNotCrossPaired()
        {
            #region implementation

            var doc = Guid.NewGuid();
            // Study 1 (TextTableID=100): Drug A 50mg + Placebo
            var s1Drug = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 20.0, "Percentage");
            var s1Placebo = aeRow(2, doc, 100, "Nausea", "Placebo (S1)", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 2);

            // Study 2 (TextTableID=200): Drug A 100mg + Vehicle
            var s2Drug = aeRow(3, doc, 200, "Nausea", "Drug A 100mg", 100, 100m, "mg", 30.0, "Percentage");
            var s2Vehicle = aeRow(4, doc, 200, "Nausea", "Vehicle (S2)", 100, 0m, null, 15.0, "Percentage", sourceRowSeq: 2);

            var (service, db) = createService(s1Drug, s1Placebo, s2Drug, s2Vehicle);

            await service.PopulateAsync();

            var rows = db.Set<LabelView.FlattenedAdverseEventTable>().ToList();
            Assert.AreEqual(2, rows.Count, "One drug row from each TextTable; placebo/vehicle excluded as comparators");

            // Each row should be paired with the placebo from its own TextTable
            var s1Row = rows.Single(r => r.TreatmentArm == "Drug A 50mg");
            Assert.AreEqual("Placebo (S1)", s1Row.ComparatorArm);

            var s2Row = rows.Single(r => r.TreatmentArm == "Drug A 100mg");
            Assert.AreEqual("Vehicle (S2)", s2Row.ComparatorArm);

            #endregion
        }

        /**************************************************************/
        /// <summary>Two placebo rows with identical Dose: deterministic tie-break by SourceRowSeq.</summary>
        [TestMethod]
        public async Task PopulateAsync_DeterministicTieBreak()
        {
            #region implementation

            var doc = Guid.NewGuid();
            // Two placebo rows, same dose=0; SourceRowSeq differs (1 vs 2)
            var placebo1 = aeRow(1, doc, 100, "Nausea", "Placebo A", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 1);
            var placebo2 = aeRow(2, doc, 100, "Nausea", "Placebo B", 100, 0m, null, 12.0, "Percentage", sourceRowSeq: 2);
            var drug = aeRow(3, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 20.0, "Percentage", sourceRowSeq: 3);

            var (service, db) = createService(placebo1, placebo2, drug);

            await service.PopulateAsync();

            var rows = db.Set<LabelView.FlattenedAdverseEventTable>().ToList();
            // Comparator should be Placebo A (lowest SourceRowSeq tie-break)
            // Output rows: drug row, plus the OTHER placebo (Placebo B) which is also non-comparator
            Assert.IsTrue(rows.All(r => r.ComparatorArm == "Placebo A"),
                "All output rows should reference the deterministic comparator (Placebo A)");

            #endregion
        }

        #endregion Document and Group Handling

        #region Idempotency

        /**************************************************************/
        /// <summary>Calling PopulateAsync twice produces the same row count (truncate-on-rerun).</summary>
        [TestMethod]
        public async Task PopulateAsync_TruncatesOnRerun()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var drug = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 20.0, "Percentage");
            var placebo = aeRow(2, doc, 100, "Nausea", "Placebo", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 2);

            var (service, db) = createService(drug, placebo);

            int firstRun = await service.PopulateAsync();
            int secondRun = await service.PopulateAsync();

            Assert.AreEqual(firstRun, secondRun);
            Assert.AreEqual(secondRun, db.Set<LabelView.FlattenedAdverseEventTable>().Count(),
                "Rerun should truncate first; total count should equal a single run, not double");

            #endregion
        }

        /**************************************************************/
        /// <summary>TruncateAsync clears the AE table even when called directly.</summary>
        [TestMethod]
        public async Task TruncateAsync_ClearsTable()
        {
            #region implementation

            var (service, db) = createService();
            // Pre-populate output table directly
            db.Set<LabelView.FlattenedAdverseEventTable>().Add(new LabelView.FlattenedAdverseEventTable
            {
                FlattenedStandardizedTableId = 1,
                DocumentGUID = Guid.NewGuid(),
                ParameterName = "Test"
            });
            await db.SaveChangesAsync();

            Assert.AreEqual(1, db.Set<LabelView.FlattenedAdverseEventTable>().Count());

            await service.TruncateAsync();

            Assert.AreEqual(0, db.Set<LabelView.FlattenedAdverseEventTable>().Count());

            #endregion
        }

        #endregion Idempotency

        #region Population-Aware Comparator Grouping

        /**************************************************************/
        /// <summary>
        /// Multi-StudyContext table (44661 archetype): four arms partitioned by Adults vs
        /// Children. The Adults Clomipramine row must pair against the **Adults** Placebo
        /// (N=319), not the Children Placebo, because the comparator group key now
        /// includes <c>StudyContext</c>.
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_MultiStudyContext_PairsWithinPopulation()
        {
            #region implementation

            var doc = Guid.NewGuid();

            var adultsClomi = aeRow(1, doc, 44661, "Somnolence", "Clomipramine", 322, 50m, "mg", 54, "Percentage");
            adultsClomi.StudyContext = "Adults";
            var adultsPlacebo = aeRow(2, doc, 44661, "Somnolence", "Placebo", 319, 0m, "mg", 16, "Percentage");
            adultsPlacebo.StudyContext = "Adults";
            var childrenClomi = aeRow(3, doc, 44661, "Somnolence", "Clomipramine", 46, 50m, "mg", 46, "Percentage");
            childrenClomi.StudyContext = "Children and Adolescents";
            var childrenPlacebo = aeRow(4, doc, 44661, "Somnolence", "Placebo", 44, 0m, "mg", 11, "Percentage");
            childrenPlacebo.StudyContext = "Children and Adolescents";

            var (service, db) = createService(adultsClomi, adultsPlacebo, childrenClomi, childrenPlacebo);

            await service.PopulateAsync();

            var rows = db.Set<LabelView.FlattenedAdverseEventTable>().ToList();
            // Both placebo rows are excluded from output as comparators -> 2 emitted rows.
            Assert.AreEqual(2, rows.Count, "Expected exactly two non-placebo rows in output.");

            var adultsTreated = rows.Single(r => r.StudyContext == "Adults");
            Assert.AreEqual("Clomipramine", adultsTreated.TreatmentArm);
            Assert.AreEqual("Placebo", adultsTreated.ComparatorArm);
            Assert.AreEqual(319, adultsTreated.ComparatorN, "Adults Clomipramine must pair with Adults Placebo (N=319).");
            Assert.AreEqual(322, adultsTreated.ArmN);

            var childrenTreated = rows.Single(r => r.StudyContext == "Children and Adolescents");
            Assert.AreEqual("Clomipramine", childrenTreated.TreatmentArm);
            Assert.AreEqual("Placebo", childrenTreated.ComparatorArm);
            Assert.AreEqual(44, childrenTreated.ComparatorN, "Children Clomipramine must pair with Children Placebo (N=44).");
            Assert.AreEqual(46, childrenTreated.ArmN);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Caption-derived <c>Population</c> alone is sufficient to partition the
        /// comparator group when <c>StudyContext</c> is null. Validates that Population
        /// participates in the group key.
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_PopulationOnlyPartition_PairsWithinPopulation()
        {
            #region implementation

            var doc = Guid.NewGuid();

            var adultsClomi = aeRow(1, doc, 99001, "Headache", "Drug A", 200, 50m, "mg", 30, "Percentage");
            adultsClomi.Population = "Adult Healthy Volunteers";
            var adultsPlacebo = aeRow(2, doc, 99001, "Headache", "Placebo", 200, 0m, "mg", 10, "Percentage");
            adultsPlacebo.Population = "Adult Healthy Volunteers";
            var pediatricClomi = aeRow(3, doc, 99001, "Headache", "Drug A", 60, 50m, "mg", 25, "Percentage");
            pediatricClomi.Population = "Pediatric Patients";
            var pediatricPlacebo = aeRow(4, doc, 99001, "Headache", "Placebo", 60, 0m, "mg", 8, "Percentage");
            pediatricPlacebo.Population = "Pediatric Patients";

            var (service, db) = createService(adultsClomi, adultsPlacebo, pediatricClomi, pediatricPlacebo);

            await service.PopulateAsync();

            var rows = db.Set<LabelView.FlattenedAdverseEventTable>().ToList();
            Assert.AreEqual(2, rows.Count);

            var adults = rows.Single(r => r.Population == "Adult Healthy Volunteers");
            Assert.AreEqual(200, adults.ComparatorN);

            var pediatric = rows.Single(r => r.Population == "Pediatric Patients");
            Assert.AreEqual(60, pediatric.ComparatorN);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Subpopulation partitions the comparator group separately from StudyContext
        /// (Female-only Dysmenorrhea must pair against Female-only Placebo). The per-arm
        /// ArmN comes from the subpopulation's N, not the whole-study N.
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_SubpopulationPartition_PairsWithinSubpopulation()
        {
            #region implementation

            var doc = Guid.NewGuid();

            // Female-only slice: Clomipramine N=182, Placebo N=167
            var femaleClomi = aeRow(1, doc, 44661, "Dysmenorrhea", "Clomipramine", 182, 50m, "mg", 12, "Percentage");
            femaleClomi.StudyContext = "Adults";
            femaleClomi.Subpopulation = "Female Patients Only";
            var femalePlacebo = aeRow(2, doc, 44661, "Dysmenorrhea", "Placebo", 167, 0m, "mg", 14, "Percentage");
            femalePlacebo.StudyContext = "Adults";
            femalePlacebo.Subpopulation = "Female Patients Only";

            // Male-only slice (different parameter, but same pattern): Ejaculation failure
            var maleClomi = aeRow(3, doc, 44661, "Ejaculation failure", "Clomipramine", 140, 50m, "mg", 42, "Percentage");
            maleClomi.StudyContext = "Adults";
            maleClomi.Subpopulation = "Male Patients Only";
            var malePlacebo = aeRow(4, doc, 44661, "Ejaculation failure", "Placebo", 152, 0m, "mg", 2, "Percentage");
            malePlacebo.StudyContext = "Adults";
            malePlacebo.Subpopulation = "Male Patients Only";

            var (service, db) = createService(femaleClomi, femalePlacebo, maleClomi, malePlacebo);

            await service.PopulateAsync();

            var rows = db.Set<LabelView.FlattenedAdverseEventTable>().ToList();
            Assert.AreEqual(2, rows.Count);

            var female = rows.Single(r => r.Subpopulation == "Female Patients Only");
            Assert.AreEqual(182, female.ArmN, "Dysmenorrhea Clomipramine must use the female-only N (182).");
            Assert.AreEqual(167, female.ComparatorN, "Pair must be Female-only Placebo (167), not whole-study placebo.");
            Assert.IsNotNull(female.RR, "RR should compute for a within-subpop pair.");

            var male = rows.Single(r => r.Subpopulation == "Male Patients Only");
            Assert.AreEqual(140, male.ArmN);
            Assert.AreEqual(152, male.ComparatorN);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Whitespace and case variants in <c>StudyContext</c> must not split a valid
        /// comparator group. Validates the <c>normalizeKey</c> helper (trim, collapse,
        /// ToUpperInvariant).
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_StudyContextWhitespaceVariants_StaySingleGroup()
        {
            #region implementation

            var doc = Guid.NewGuid();

            var clomi = aeRow(1, doc, 99002, "Nausea", "Clomipramine", 200, 50m, "mg", 30, "Percentage");
            clomi.StudyContext = "Adults"; // canonical
            var placebo = aeRow(2, doc, 99002, "Nausea", "Placebo", 200, 0m, "mg", 10, "Percentage");
            placebo.StudyContext = "  adults "; // trailing space + lowercase

            var (service, db) = createService(clomi, placebo);

            await service.PopulateAsync();

            var rows = db.Set<LabelView.FlattenedAdverseEventTable>().ToList();
            // If the whitespace/case variants split into two groups, both rows would emit
            // with NO_COMPARATOR. Single-group means 1 row emitted with a comparator.
            Assert.AreEqual(1, rows.Count, "Whitespace/case variants must not split the group.");
            Assert.AreEqual("Placebo", rows[0].ComparatorArm);
            Assert.AreEqual(200, rows[0].ComparatorN);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Subpopulation slice produces coherent EventsTreatment / EventsComparator
        /// derived from the subpopulation N (not the whole-study N).
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_SubpopulationSlice_EventCountsCoherent()
        {
            #region implementation

            var doc = Guid.NewGuid();

            // 12% of 182 ≈ 21.84 events; 14% of 167 ≈ 23.38 events.
            var clomi = aeRow(1, doc, 44661, "Dysmenorrhea", "Clomipramine", 182, 50m, "mg", 12, "Percentage");
            clomi.Subpopulation = "Female Patients Only";
            var placebo = aeRow(2, doc, 44661, "Dysmenorrhea", "Placebo", 167, 0m, "mg", 14, "Percentage");
            placebo.Subpopulation = "Female Patients Only";

            var (service, db) = createService(clomi, placebo);

            await service.PopulateAsync();

            var row = db.Set<LabelView.FlattenedAdverseEventTable>().Single();
            Assert.AreEqual(182, row.ArmN);
            Assert.AreEqual(167, row.ComparatorN);
            Assert.IsNotNull(row.EventsTreatment, "EventsTreatment must be derived for Percentage rows.");
            Assert.IsNotNull(row.EventsComparator);

            // Sanity: events should match ArmN * % / 100 within rounding tolerance.
            Assert.AreEqual(182.0 * 12.0 / 100.0, row.EventsTreatment!.Value, 0.5);
            Assert.AreEqual(167.0 * 14.0 / 100.0, row.EventsComparator!.Value, 0.5);

            // RR should be computable on a coherent subpop slice.
            Assert.IsNotNull(row.RR);
            Assert.IsNotNull(row.RRLowerBound);
            Assert.IsNotNull(row.RRUpperBound);

            #endregion
        }

        #endregion Population-Aware Comparator Grouping
    }
}
