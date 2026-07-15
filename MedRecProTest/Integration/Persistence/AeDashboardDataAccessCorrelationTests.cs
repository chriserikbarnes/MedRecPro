using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using MedRecPro.Service;
using MedRecPro.Service.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Integration.Persistence
{
    /**************************************************************/
    /// <summary>
    /// Contains AE dashboard SOC correlation-map, heatmap, cell-detail, and helper-policy tests.
    /// </summary>
    /// <remarks>
    /// This partial preserves the original AE dashboard test identities and setup.
    /// </remarks>
    /// <seealso cref="AeDashboardDataAccessTests"/>
    public partial class AeDashboardDataAccessTests
    {
        #region correlation map tests

        /**************************************************************/
        /// <summary>
        /// Verifies that two SOCs with identical per-drug LogRR ranks correlate at +1.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_PerfectlyCorrelatedSocs_ReturnsCoefficientOne()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // Three placebo drugs; identical per-SOC RR so the two SOCs share one rank order.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 5, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socA, rr: 8.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 6, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socB, rr: 8.0);

            var map = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, minDrugsPerCell: 3);

            Assert.IsNotNull(map);
            Assert.AreEqual(2, map.Soc.Count);
            Assert.AreEqual(3, map.DrugCount);
            var offDiagonal = map.Cells.Single(cell => !cell.IsDiagonal);
            Assert.AreEqual(3, offDiagonal.PairCount);
            Assert.IsFalse(offDiagonal.InsufficientN);
            Assert.IsNotNull(offDiagonal.Coefficient);
            Assert.AreEqual(1.0, offDiagonal.Coefficient!.Value, 1e-9);
            Assert.IsTrue(map.Cells.Where(cell => cell.IsDiagonal).All(cell => cell.Coefficient == 1.0));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that two SOCs with reversed per-drug LogRR ranks correlate at -1.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_AntiCorrelatedSocs_ReturnsCoefficientNegativeOne()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // SOC A ascends 2,4,8 while SOC B descends 8,4,2 for the same drugs.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 8.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 5, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socA, rr: 8.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 6, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socB, rr: 2.0);

            var map = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, minDrugsPerCell: 3);

            Assert.IsNotNull(map);
            var offDiagonal = map.Cells.Single(cell => !cell.IsDiagonal);
            Assert.AreEqual(3, offDiagonal.PairCount);
            Assert.IsNotNull(offDiagonal.Coefficient);
            Assert.AreEqual(-1.0, offDiagonal.Coefficient!.Value, 1e-9);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a cell below the drugs-per-cell floor returns a null coefficient and the real n.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_BelowMinDrugsPerCell_ReturnsNullInsufficientN()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // Only two drugs overlap the pair, below the default floor of four.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 4.0);

            var map = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger);

            Assert.IsNotNull(map);
            var offDiagonal = map.Cells.Single(cell => !cell.IsDiagonal);
            Assert.AreEqual(2, offDiagonal.PairCount);
            Assert.IsTrue(offDiagonal.InsufficientN);
            Assert.IsNull(offDiagonal.Coefficient);
            Assert.IsTrue(map.Warnings.Any(warning => warning.Contains("minimum")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a SOC with constant LogRR across drugs yields a null (not NaN) coefficient.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_ZeroVarianceSoc_ReturnsNullCoefficient()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // SOC A varies; SOC B is constant (rr 3.0) for all four drugs.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 3.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 3.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 5, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socA, rr: 6.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 6, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socB, rr: 3.0);
            seedCorrelationRow(connection, drugDoc(4), riskId: 7, activeMoietyId: 104, pharmClassCode: code, parameterCategory: socA, rr: 8.0);
            seedCorrelationRow(connection, drugDoc(4), riskId: 8, activeMoietyId: 104, pharmClassCode: code, parameterCategory: socB, rr: 3.0);

            var map = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, minDrugsPerCell: 3);

            Assert.IsNotNull(map);
            var offDiagonal = map.Cells.Single(cell => !cell.IsDiagonal);
            Assert.AreEqual(4, offDiagonal.PairCount);
            Assert.IsFalse(offDiagonal.InsufficientN);
            Assert.IsNull(offDiagonal.Coefficient);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the comparator defaults to placebo and excludes active-comparator rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_ComparatorDefaultsToPlacebo_ExcludesActiveRows()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string placeboSocA = "Skin and Subcutaneous Tissue Disorders";
            const string placeboSocB = "Gastrointestinal Disorders";
            const string activeSocC = "Nervous System Disorders";
            const string activeSocD = "Psychiatric Disorders";

            // Placebo drugs populate SOC A/B; active-only drugs populate SOC C/D.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: placeboSocA, rr: 2.0, isPlaceboControlled: true);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: placeboSocB, rr: 2.0, isPlaceboControlled: true);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: placeboSocA, rr: 4.0, isPlaceboControlled: true);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: placeboSocB, rr: 4.0, isPlaceboControlled: true);
            seedCorrelationRow(connection, drugDoc(3), riskId: 5, activeMoietyId: 103, pharmClassCode: code, parameterCategory: activeSocC, rr: 3.0, isPlaceboControlled: false);
            seedCorrelationRow(connection, drugDoc(3), riskId: 6, activeMoietyId: 103, pharmClassCode: code, parameterCategory: activeSocD, rr: 3.0, isPlaceboControlled: false);
            seedCorrelationRow(connection, drugDoc(4), riskId: 7, activeMoietyId: 104, pharmClassCode: code, parameterCategory: activeSocC, rr: 5.0, isPlaceboControlled: false);
            seedCorrelationRow(connection, drugDoc(4), riskId: 8, activeMoietyId: 104, pharmClassCode: code, parameterCategory: activeSocD, rr: 5.0, isPlaceboControlled: false);

            var placeboMap = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, minDrugsPerCell: 3);
            var activeMap = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, comparator: AeComparatorMix.Active, minDrugsPerCell: 3);

            Assert.IsNotNull(placeboMap);
            Assert.IsTrue(placeboMap.Soc.Contains(placeboSocA));
            Assert.IsFalse(placeboMap.Soc.Contains(activeSocC));
            Assert.IsNotNull(activeMap);
            Assert.IsTrue(activeMap.Soc.Contains(activeSocC));
            Assert.IsFalse(activeMap.Soc.Contains(placeboSocA));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that excluding non-significant rows drops them before correlating.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_ExcludeNonSignificant_DropsNotSignificantInputRows()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";
            const string socC = "Nervous System Disorders";

            // SOC A/B are elevated; SOC C carries only not-significant rows.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0, significance: "elevated");
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 2.0, significance: "elevated");
            seedCorrelationRow(connection, drugDoc(1), riskId: 3, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socC, rr: 1.1, significance: "not significant");
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0, significance: "elevated");
            seedCorrelationRow(connection, drugDoc(2), riskId: 5, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 4.0, significance: "elevated");
            seedCorrelationRow(connection, drugDoc(2), riskId: 6, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socC, rr: 1.1, significance: "not significant");

            var included = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, minDrugsPerCell: 3);
            var excluded = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, includeNonSignificant: false, minDrugsPerCell: 3);

            Assert.IsNotNull(included);
            Assert.IsTrue(included.Soc.Contains(socC));
            Assert.IsNotNull(excluded);
            Assert.IsFalse(excluded.Soc.Contains(socC));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that fragile rows are excluded by default and surfaced when fragile is included.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_ExcludeFragile_DropsFragileInputRows()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // SOC A is sound; every SOC B row is fragile via a zero-cell correction flag.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 2.0, calculationFlags: "ZERO_CELL_CORRECTED");
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 4.0, calculationFlags: "ZERO_CELL_CORRECTED");
            seedCorrelationRow(connection, drugDoc(3), riskId: 5, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socA, rr: 8.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 6, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socB, rr: 8.0, calculationFlags: "ZERO_CELL_CORRECTED");

            var strict = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, minDrugsPerCell: 3);
            var lenient = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, excludeFragile: false, minDrugsPerCell: 3);

            Assert.IsNotNull(strict);
            Assert.IsFalse(strict.Soc.Contains(socB));
            Assert.IsNotNull(lenient);
            Assert.IsTrue(lenient.Soc.Contains(socB));
            var offDiagonal = lenient.Cells.Single(cell => !cell.IsDiagonal);
            Assert.IsTrue(offDiagonal.IsFragile);
            Assert.IsTrue(lenient.Warnings.Any(warning => warning.Contains("Fragile")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that moiety and class context survive collapse and that DrugCount counts moieties.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_RetainsMoietyAndClassFromEntity()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "N0000175076";
            const string className = "Protein Kinase Inhibitors [MoA]";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // Two moieties under class 203; one has a duplicate stratum that must collapse to one row.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 452, pharmacologicClassId: 203, pharmClassCode: code, pharmClassName: className, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 452, pharmacologicClassId: 203, pharmClassCode: code, pharmClassName: className, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 3, activeMoietyId: 452, pharmacologicClassId: 203, pharmClassCode: code, pharmClassName: className, parameterCategory: socB, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 853, pharmacologicClassId: 203, pharmClassCode: code, pharmClassName: className, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 5, activeMoietyId: 853, pharmacologicClassId: 203, pharmClassCode: code, pharmClassName: className, parameterCategory: socB, rr: 4.0);

            var map = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, minDrugsPerCell: 3);

            Assert.IsNotNull(map);
            Assert.AreEqual(className, map.PharmClassName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(map.EncryptedPharmacologicClassID));
            Assert.AreEqual(203, map.PharmacologicClassID);
            Assert.AreEqual(2, map.DrugCount);
            var socASummary = map.SocSummaries.Single(summary => summary.Soc == socA);
            Assert.AreEqual(2, socASummary.DrugCount);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that an unknown pharmacologic class returns null (controller 404).
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_UnknownClass_ReturnsNull()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: "TESTKINASE", parameterCategory: "Skin and Subcutaneous Tissue Disorders", rr: 2.0);

            var map = await DtoLabelAccess.GetAeCorrelationMapAsync(context, "NO_SUCH_CLASS", PkSecret, logger);

            Assert.IsNull(map);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the method parameter is honored: Spearman is 1 and Pearson is below 1 on monotonic-nonlinear data.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_SpearmanVsPearson_MethodParamHonored()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // SOC A LogRR = 1,2,3,4; SOC B LogRR = 1,4,9,16 (monotonic but non-linear).
            double[] logA = { 1, 2, 3, 4 };
            double[] logB = { 1, 4, 9, 16 };
            for (var i = 0; i < 4; i++)
            {
                var moiety = 101 + i;
                seedCorrelationRow(connection, drugDoc(i + 1), riskId: i * 2 + 1, activeMoietyId: moiety, pharmClassCode: code, parameterCategory: socA, rr: Math.Exp(logA[i]));
                seedCorrelationRow(connection, drugDoc(i + 1), riskId: i * 2 + 2, activeMoietyId: moiety, pharmClassCode: code, parameterCategory: socB, rr: Math.Exp(logB[i]));
            }

            var spearman = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, method: AeCorrelationMethod.Spearman, minDrugsPerCell: 3);
            var pearson = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, method: AeCorrelationMethod.Pearson, minDrugsPerCell: 3);

            Assert.IsNotNull(spearman);
            Assert.IsNotNull(pearson);
            Assert.AreEqual(AeCorrelationMethod.Spearman, spearman.AppliedFilters.Method);
            Assert.AreEqual(AeCorrelationMethod.Pearson, pearson.AppliedFilters.Method);
            var spearmanCell = spearman.Cells.Single(cell => !cell.IsDiagonal);
            var pearsonCell = pearson.Cells.Single(cell => !cell.IsDiagonal);
            Assert.AreEqual(1.0, spearmanCell.Coefficient!.Value, 1e-9);
            Assert.IsTrue(pearsonCell.Coefficient!.Value < 0.999);
            Assert.IsTrue(pearsonCell.Coefficient!.Value > 0.95);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the class picker scopes to AE classes and flags which can render map cells.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationClassesAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationClassesAsync_ScopesToAeClasses_AndFlagsRenderableMap()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // A map-ready class: four drugs each have both SOCs, meeting the default four-drug floor.
            for (var i = 0; i < 4; i++)
            {
                var moiety = 101 + i;
                seedCorrelationRow(connection, drugDoc(i + 1), riskId: i * 2 + 1, activeMoietyId: moiety, pharmacologicClassId: 500, pharmClassCode: "READYCLASS", parameterCategory: socA, rr: 2.0 + i);
                seedCorrelationRow(connection, drugDoc(i + 1), riskId: i * 2 + 2, activeMoietyId: moiety, pharmacologicClassId: 500, pharmClassCode: "READYCLASS", parameterCategory: socB, rr: 3.0 + i);
            }

            // A broad but not map-ready class: drugs and SOCs exist, but no drug has both SOCs.
            seedCorrelationRow(connection, drugDoc(10), riskId: 101, activeMoietyId: 201, pharmacologicClassId: 600, pharmClassCode: "NOMAPCLASS", parameterCategory: socA, rr: 3.0);
            seedCorrelationRow(connection, drugDoc(11), riskId: 102, activeMoietyId: 202, pharmacologicClassId: 600, pharmClassCode: "NOMAPCLASS", parameterCategory: socB, rr: 4.0);
            // A single-drug, single-SOC class.
            seedCorrelationRow(connection, drugDoc(12), riskId: 103, activeMoietyId: 301, pharmacologicClassId: 700, pharmClassCode: "SINGLECLASS", parameterCategory: socA, rr: 3.0);

            var page = await DtoLabelAccess.GetAeCorrelationClassesAsync(context, PkSecret, logger);
            var classes = page.Items;

            Assert.AreEqual(3, page.TotalCount);
            Assert.AreEqual(1, page.ChartableCount);
            Assert.AreEqual("READYCLASS", classes[0].PharmClassCode);
            var renderable = classes.Single(item => item.PharmClassCode == "READYCLASS");
            var noMap = classes.Single(item => item.PharmClassCode == "NOMAPCLASS");
            var single = classes.Single(item => item.PharmClassCode == "SINGLECLASS");
            Assert.IsTrue(renderable.HasRenderableMap);
            Assert.IsTrue(renderable.IsCorrelatable);
            Assert.AreEqual(4, renderable.DrugCount);
            Assert.AreEqual(2, renderable.SocCount);
            Assert.AreEqual(1, renderable.TotalOffDiagonalCellCount);
            Assert.AreEqual(1, renderable.UsableMapCellCount);
            Assert.AreEqual(4, renderable.MaxPairCount);
            Assert.IsNull(renderable.RenderabilityReason);
            Assert.IsFalse(noMap.HasRenderableMap);
            Assert.IsFalse(noMap.IsCorrelatable);
            Assert.AreEqual(1, noMap.TotalOffDiagonalCellCount);
            Assert.AreEqual(0, noMap.UsableMapCellCount);
            Assert.IsTrue(noMap.RenderabilityReason!.Contains("4-drug floor"));
            Assert.AreEqual(0, single.TotalOffDiagonalCellCount);
            Assert.IsFalse(single.IsCorrelatable);
            Assert.IsFalse(string.IsNullOrWhiteSpace(renderable.EncryptedPharmacologicClassID));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the heatmap builds a SOC × drug grid with aggregated values and dedups to moiety.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationHeatmapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationHeatmapAsync_BuildsSocByDrugGrid()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // Drug 101 has two terms in SOC A so its cell aggregates two rows.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, parameterName: "Rash", rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, parameterName: "Pruritus", rr: 3.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 3, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, parameterName: "Nausea", rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, parameterName: "Rash", rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 5, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, parameterName: "Nausea", rr: 4.0);

            var heatmap = await DtoLabelAccess.GetAeCorrelationHeatmapAsync(context, code, PkSecret, logger);

            Assert.IsNotNull(heatmap);
            Assert.AreEqual(2, heatmap.Soc.Count);
            Assert.AreEqual(2, heatmap.Drugs.Count);
            Assert.IsTrue(heatmap.Cells.Count > 0);
            Assert.IsTrue(heatmap.Cells.All(cell => cell.LogRr.HasValue && cell.Rr.HasValue && cell.Precision.HasValue));
            Assert.IsTrue(heatmap.Cells.Any(cell => cell.TermCount == 2));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the cell drill-down returns the per-drug paired observations with provenance.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationCellDetailAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationCellDetailAsync_ReturnsPerDrugPairs()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 5, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socA, rr: 8.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 6, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socB, rr: 8.0);

            // minDrugsPerCell:3 keeps the three-drug cell at or above the floor so the map-safe
            // coefficient is populated; the raw coefficient mirrors it.
            var detail = await DtoLabelAccess.GetAeCorrelationCellDetailAsync(context, code, socA, socB, PkSecret, logger, minDrugsPerCell: 3);

            Assert.IsNotNull(detail);
            Assert.AreEqual(3, detail.PairCount);
            Assert.AreEqual(3, detail.DrugPairs.Count);
            Assert.IsTrue(detail.DrugPairs.All(pair => pair.LogRrX.HasValue && pair.LogRrY.HasValue));
            Assert.IsTrue(detail.DrugPairs.All(pair => !string.IsNullOrWhiteSpace(pair.EncryptedActiveMoietyID)));
            Assert.IsFalse(detail.InsufficientN);
            Assert.IsNotNull(detail.Coefficient);
            Assert.AreEqual(1.0, detail.Coefficient!.Value, 1e-9);
            Assert.IsNotNull(detail.RawCoefficient);
            Assert.AreEqual(1.0, detail.RawCoefficient!.Value, 1e-9);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the serious-SOC and exclude-combos axis filters restrict the data.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_SeriousSocOnly_And_ExcludeCombos_FilterAxis()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string seriousSoc = "Cardiac Disorders";
            const string benignSoc = "General Disorders and Administration Site Conditions";

            // Two mono drugs and one combo drug, each in a serious and a non-serious SOC.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: seriousSoc, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: benignSoc, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: seriousSoc, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: benignSoc, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 5, activeMoietyId: 103, pharmClassCode: code, parameterCategory: seriousSoc, rr: 8.0, isCombo: true);
            seedCorrelationRow(connection, drugDoc(3), riskId: 6, activeMoietyId: 103, pharmClassCode: code, parameterCategory: benignSoc, rr: 8.0, isCombo: true);

            var seriousOnly = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, seriousSocOnly: true, minDrugsPerCell: 3);
            var noCombos = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, excludeCombos: true, minDrugsPerCell: 3);

            Assert.IsNotNull(seriousOnly);
            Assert.IsTrue(seriousOnly.Soc.Contains(seriousSoc));
            Assert.IsFalse(seriousOnly.Soc.Contains(benignSoc));
            Assert.IsNotNull(noCombos);
            Assert.AreEqual(2, noCombos.DrugCount);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the pure correlation math against hand calculations for known vectors.
        /// </summary>
        /// <seealso cref="AeDashboardDerivation.ComputeCorrelation"/>
        /// <seealso cref="AeDashboardDerivation.StudentTTwoSidedP"/>
        [TestMethod]
        public void ComputeCorrelation_KnownVectors_MatchHandCalc()
        {
            #region implementation

            // Perfect positive linear relationship: r = 1 for both methods.
            var positivePearson = AeDashboardDerivation.ComputeCorrelation(new[] { 1.0, 2.0, 3.0 }, new[] { 2.0, 4.0, 6.0 }, AeCorrelationMethod.Pearson);
            var positiveSpearman = AeDashboardDerivation.ComputeCorrelation(new[] { 1.0, 2.0, 3.0 }, new[] { 2.0, 4.0, 6.0 }, AeCorrelationMethod.Spearman);
            Assert.AreEqual(3, positivePearson.PairCount);
            Assert.AreEqual(1.0, positivePearson.Coefficient!.Value, 1e-9);
            Assert.AreEqual(1.0, positiveSpearman.Coefficient!.Value, 1e-9);

            // Perfect negative relationship: r = -1.
            var negative = AeDashboardDerivation.ComputeCorrelation(new[] { 1.0, 2.0, 3.0 }, new[] { 6.0, 4.0, 2.0 }, AeCorrelationMethod.Pearson);
            Assert.AreEqual(-1.0, negative.Coefficient!.Value, 1e-9);

            // Zero variance: undefined coefficient, never NaN.
            var constant = AeDashboardDerivation.ComputeCorrelation(new[] { 1.0, 2.0, 3.0 }, new[] { 2.0, 2.0, 2.0 }, AeCorrelationMethod.Pearson);
            Assert.IsFalse(constant.Coefficient.HasValue);
            Assert.IsFalse(constant.PValue.HasValue);

            // n = 2: coefficient is defined but the p-value is not.
            var twoPoints = AeDashboardDerivation.ComputeCorrelation(new[] { 1.0, 2.0 }, new[] { 2.0, 5.0 }, AeCorrelationMethod.Pearson);
            Assert.AreEqual(2, twoPoints.PairCount);
            Assert.IsTrue(twoPoints.Coefficient.HasValue);
            Assert.IsFalse(twoPoints.PValue.HasValue);

            // Student's t two-sided p-value: t = 0 -> p = 1, the 0.05 critical value at df = 10 -> p ~ 0.05.
            Assert.AreEqual(1.0, AeDashboardDerivation.StudentTTwoSidedP(0.0, 5), 1e-9);
            Assert.AreEqual(0.05, AeDashboardDerivation.StudentTTwoSidedP(2.228, 10), 0.005);
            Assert.IsTrue(AeDashboardDerivation.StudentTTwoSidedP(10.0, 5) < 0.001);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that one drug's SOC casing variants fold into a single aggregate
        /// instead of splitting and silently overwriting in the case-insensitive pivot.
        /// </summary>
        /// <seealso cref="AeDashboardDerivation.AggregatePerDrugSoc"/>
        [TestMethod]
        public void AggregatePerDrugSoc_MixedCaseSoc_FoldsToOneAggregate()
        {
            #region implementation

            var observations = new[]
            {
                new AeCorrelationObservation { DrugKey = "moiety:1", Soc = "Cardiac Disorders", LogRr = 1.0 },
                new AeCorrelationObservation { DrugKey = "moiety:1", Soc = "Cardiac disorders", LogRr = 3.0 }
            };

            var aggregates = AeDashboardDerivation.AggregatePerDrugSoc(observations, AeCorrelationAggregation.MedianLogRr);

            var aggregate = aggregates.Single();
            Assert.AreEqual(2, aggregate.Value.Count);
            Assert.AreEqual(2.0, aggregate.Value.Value, 1e-9);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the cell drill-down suppresses the map-safe coefficient below the floor while
        /// still exposing the raw diagnostic coefficient and all contributing pairs.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationCellDetailAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationCellDetailAsync_BelowMinDrugsPerCell_ReturnsNullMapCoefficientAndRawCoefficient()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // Three drugs overlap the pair, below the default floor of four.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 5, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socA, rr: 8.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 6, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socB, rr: 8.0);

            var detail = await DtoLabelAccess.GetAeCorrelationCellDetailAsync(context, code, socA, socB, PkSecret, logger);

            Assert.IsNotNull(detail);
            Assert.AreEqual(3, detail.PairCount);
            Assert.AreEqual(4, detail.MinDrugsPerCell);
            Assert.IsTrue(detail.InsufficientN);
            Assert.IsNull(detail.Coefficient);
            Assert.IsNull(detail.PValue);
            Assert.IsNotNull(detail.RawCoefficient);
            Assert.AreEqual(1.0, detail.RawCoefficient!.Value, 1e-9);
            // RawPValue is null here not because of the floor but because a perfect ±1
            // coefficient has no defined p-value; the raw pair stays internally consistent.
            Assert.IsNull(detail.RawPValue);
            Assert.AreEqual(3, detail.DrugPairs.Count);
            Assert.IsFalse(detail.IsDiagonal);
            Assert.IsTrue(detail.Warnings.Any(warning => warning.Contains("suppressed")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that requesting the same SOC for both axes returns a non-informative diagonal cell.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationCellDetailAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationCellDetailAsync_Diagonal_ReturnsNonInformativeDiagonal()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";

            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 2, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 3, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socA, rr: 8.0);

            // Same SOC on both axes (case-insensitive) is the diagonal.
            var detail = await DtoLabelAccess.GetAeCorrelationCellDetailAsync(context, code, socA, socA.ToLowerInvariant(), PkSecret, logger);

            Assert.IsNotNull(detail);
            Assert.IsTrue(detail.IsDiagonal);
            Assert.IsNotNull(detail.Coefficient);
            Assert.AreEqual(1.0, detail.Coefficient!.Value, 1e-9);
            Assert.IsNotNull(detail.RawCoefficient);
            Assert.AreEqual(1.0, detail.RawCoefficient!.Value, 1e-9);
            Assert.IsNull(detail.PValue);
            Assert.IsFalse(detail.IsSignificant);
            Assert.IsTrue(detail.DrugPairs.Count > 0);
            Assert.IsTrue(detail.Warnings.Any(warning => warning.Contains("diagonal")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that requesting a SOC with no rows after filtering returns empty pairs, null
        /// coefficients, and an explanatory warning rather than an error.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationCellDetailAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationCellDetailAsync_MissingSoc_ReturnsEmptyPairsAndWarning()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";

            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);

            var detail = await DtoLabelAccess.GetAeCorrelationCellDetailAsync(context, code, socA, "Cardiac Disorders", PkSecret, logger);

            Assert.IsNotNull(detail);
            Assert.AreEqual(0, detail.DrugPairs.Count);
            Assert.AreEqual(0, detail.PairCount);
            Assert.IsNull(detail.Coefficient);
            Assert.IsNull(detail.RawCoefficient);
            Assert.IsNull(detail.PValue);
            Assert.IsTrue(detail.Warnings.Any(warning => warning.Contains("no rows")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the map clamps a below-floor drugs-per-cell request up to the hard floor of three and echoes it.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_MinDrugsPerCellBelowFloor_EchoesThree()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 4.0);

            var map = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, minDrugsPerCell: 1);

            Assert.IsNotNull(map);
            Assert.AreEqual(3, map.AppliedFilters.MinDrugsPerCell);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the Both comparator includes active and placebo rows and adds a mixed-estimand warning.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_BothComparator_AddsMixedComparatorWarning()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string placeboSoc = "Skin and Subcutaneous Tissue Disorders";
            const string activeSoc = "Nervous System Disorders";

            // Placebo drugs populate one SOC; active-only drugs populate another.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: placeboSoc, rr: 2.0, isPlaceboControlled: true);
            seedCorrelationRow(connection, drugDoc(2), riskId: 2, activeMoietyId: 102, pharmClassCode: code, parameterCategory: placeboSoc, rr: 4.0, isPlaceboControlled: true);
            seedCorrelationRow(connection, drugDoc(3), riskId: 3, activeMoietyId: 103, pharmClassCode: code, parameterCategory: activeSoc, rr: 3.0, isPlaceboControlled: false);
            seedCorrelationRow(connection, drugDoc(4), riskId: 4, activeMoietyId: 104, pharmClassCode: code, parameterCategory: activeSoc, rr: 5.0, isPlaceboControlled: false);

            var map = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, comparator: AeComparatorMix.Both, minDrugsPerCell: 3);

            Assert.IsNotNull(map);
            Assert.AreEqual(AeComparatorMix.Both, map.AppliedFilters.Comparator);
            Assert.IsTrue(map.Soc.Contains(placeboSoc));
            Assert.IsTrue(map.Soc.Contains(activeSoc));
            Assert.IsTrue(map.Warnings.Any(warning => warning.Contains("mix")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the class picker honors the search filter and returns total count before paging.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationClassesAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationClassesAsync_SearchAndPaging_ReturnsExpectedSliceAndTotalCount()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // Two searchable kinase classes plus one non-matching class.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmacologicClassId: 701, pharmClassCode: "KINASE-A", pharmClassName: "Alpha Kinase Inhibitors", parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 2, activeMoietyId: 102, pharmacologicClassId: 701, pharmClassCode: "KINASE-A", pharmClassName: "Alpha Kinase Inhibitors", parameterCategory: socB, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 3, activeMoietyId: 201, pharmacologicClassId: 702, pharmClassCode: "KINASE-B", pharmClassName: "Beta Kinase Inhibitors", parameterCategory: socA, rr: 3.0);
            seedCorrelationRow(connection, drugDoc(4), riskId: 4, activeMoietyId: 202, pharmacologicClassId: 702, pharmClassCode: "KINASE-B", pharmClassName: "Beta Kinase Inhibitors", parameterCategory: socB, rr: 5.0);
            seedCorrelationRow(connection, drugDoc(5), riskId: 5, activeMoietyId: 301, pharmacologicClassId: 703, pharmClassCode: "OTHER-C", pharmClassName: "Gamma Other Agents", parameterCategory: socA, rr: 6.0);

            var firstPage = await DtoLabelAccess.GetAeCorrelationClassesAsync(context, PkSecret, logger, classSearch: "Kinase", page: 1, size: 1);
            var secondPage = await DtoLabelAccess.GetAeCorrelationClassesAsync(context, PkSecret, logger, classSearch: "Kinase", page: 2, size: 1);

            // Search restricts to the two kinase classes; name ordering breaks the equal non-renderable tie.
            Assert.AreEqual(2, firstPage.TotalCount);
            Assert.AreEqual(2, secondPage.TotalCount);
            Assert.AreEqual(0, firstPage.ChartableCount);
            Assert.AreEqual(0, secondPage.ChartableCount);
            Assert.AreEqual(1, firstPage.Items.Count);
            Assert.AreEqual("KINASE-A", firstPage.Items[0].PharmClassCode);
            Assert.IsFalse(firstPage.Items[0].HasRenderableMap);
            Assert.AreEqual(1, secondPage.Items.Count);
            Assert.AreEqual("KINASE-B", secondPage.Items[0].PharmClassCode);

            #endregion
        }

        #endregion correlation map tests
    }
}
