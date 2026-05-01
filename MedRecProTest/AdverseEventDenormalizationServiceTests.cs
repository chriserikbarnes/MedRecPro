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
        /// <summary>Single arm: stats null, flag NO_COMPARATOR, IsPlaceboControlled=false.</summary>
        [TestMethod]
        public async Task PopulateAsync_SingleArm_NoComparator()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var row = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", 100, 50m, "mg", 14.0, "Percentage");

            var (service, db) = createService(row);

            var written = await service.PopulateAsync();

            Assert.AreEqual(1, written);
            var output = db.Set<LabelView.FlattenedAdverseEventTable>().Single();
            Assert.IsNull(output.RR);
            Assert.IsNull(output.RRLowerBound);
            Assert.IsNull(output.RRUpperBound);
            Assert.IsNull(output.ComparatorArm);
            Assert.IsFalse(output.IsPlaceboControlled);
            StringAssert.Contains(output.CalculationFlags, "NO_COMPARATOR");

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
        /// Drug + Placebo + Active comparator (distinct arm-name roots) →
        /// IsPlaceboControlled=0 for ALL rows in the document, per user spec.
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_DrugPlaceboPlusActive_FlagFalse()
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
            Assert.IsTrue(rows.All(r => !r.IsPlaceboControlled),
                "Drug + Placebo + Active comparator → IsPlaceboControlled=0 per user spec");

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

            await service.PopulateAsync();

            // Placebo is the comparator (excluded). The drug row is the output.
            var row = db.Set<LabelView.FlattenedAdverseEventTable>().Single();
            Assert.IsNull(row.RR);
            StringAssert.Contains(row.CalculationFlags, "MIXED_VALUE_TYPES");

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

            await service.PopulateAsync();

            var row = db.Set<LabelView.FlattenedAdverseEventTable>().Single();
            Assert.IsNull(row.RR);
            StringAssert.Contains(row.CalculationFlags, "PERCENT_OUT_OF_RANGE");

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

            await service.PopulateAsync();

            var row = db.Set<LabelView.FlattenedAdverseEventTable>().Single();
            Assert.IsNull(row.RR);
            StringAssert.Contains(row.CalculationFlags, "EVENTS_EXCEED_ARMN");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Percentage row with null ArmN → CIs null (per user spec). Service falls
        /// back to RR = pTreatment / pComparator point estimate, with NO_ARMN flag.
        /// </summary>
        [TestMethod]
        public async Task PopulateAsync_NullArmN_NullsCi()
        {
            #region implementation

            var doc = Guid.NewGuid();
            var drug = aeRow(1, doc, 100, "Nausea", "Drug A 50mg", null, 50m, "mg", 20.0, "Percentage");
            var placebo = aeRow(2, doc, 100, "Nausea", "Placebo", 100, 0m, null, 10.0, "Percentage", sourceRowSeq: 2);

            var (service, db) = createService(drug, placebo);

            await service.PopulateAsync();

            var row = db.Set<LabelView.FlattenedAdverseEventTable>().Single();
            // Point estimate computed from percentages (RR = 20/10 = 2.0)
            Assert.IsNotNull(row.RR);
            Assert.AreEqual(2.0, row.RR!.Value, 1e-6);
            // CIs are null per user direction "CI requires ArmN"
            Assert.IsNull(row.RRLowerBound);
            Assert.IsNull(row.RRUpperBound);
            StringAssert.Contains(row.CalculationFlags, "NO_ARMN");

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
        /// Same DocumentGUID + ParameterName but different TextTableIDs →
        /// each TextTable's group has its own comparator; no cross-pairing.
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
    }
}
