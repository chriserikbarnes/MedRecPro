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
    /// Contains AE dashboard system and pharmacologic-class correlation tests.
    /// </summary>
    /// <remarks>
    /// This partial preserves the original AE dashboard test identities and setup.
    /// </remarks>
    /// <seealso cref="AeDashboardDataAccessTests"/>
    public partial class AeDashboardDataAccessTests
    {
        #region system correlation tests

        /**************************************************************/
        /// <summary>
        /// Verifies that the MedDRA system picker scopes to systems and flags renderable class maps.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationSystemsAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationSystemsAsync_ScopesToAeSystems_AndFlagsRenderableMap()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string socA = "Cardiac Disorders";
            const string socB = "Vascular Disorders";
            var terms = new[] { "Tachycardia", "Palpitations", "Chest pain" };
            var riskId = 1;
            foreach (var term in terms)
            {
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 100 + riskId, "CLASS-A", socA, 2.0 + riskId, parameterName: term);
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 200 + riskId, "CLASS-B", socA, 3.0 + riskId, parameterName: term);
            }

            seedCorrelationRow(connection, drugDoc(99), riskId: 99, activeMoietyId: 999, pharmClassCode: "CLASS-C", parameterCategory: socB, rr: 2.0, parameterName: "Flushing");

            var page = await DtoLabelAccess.GetAeCorrelationSystemsAsync(context, PkSecret, logger, systemSearch: "cardiac", minTermsPerCell: 3);
            var system = page.Items.Single();

            Assert.AreEqual(1, page.TotalCount);
            Assert.AreEqual(1, page.ChartableCount);
            Assert.AreEqual(socA, system.SystemOrganClass);
            Assert.AreEqual(2, system.ClassCount);
            Assert.AreEqual(3, system.TermCount);
            Assert.IsTrue(system.HasRenderableMap);
            Assert.AreEqual(1, system.UsableMapCellCount);
            Assert.AreEqual(3, system.MaxPairCount);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that pharmacologic class-type helpers extract suffixes and normalize filter tokens.
        /// </summary>
        /// <seealso cref="AeDashboardDerivation.ExtractPharmacologicClassType"/>
        /// <seealso cref="AeDashboardDerivation.NormalizePharmacologicClassTypeFilter"/>
        [TestMethod]
        public void PharmacologicClassTypeHelpers_NormalizeSuffixesAndFilters()
        {
            #region implementation

            Assert.AreEqual("EPC", AeDashboardDerivation.ExtractPharmacologicClassType("Kinase Inhibitor [EPC]"));
            Assert.AreEqual("MOA", AeDashboardDerivation.ExtractPharmacologicClassType("Protein Kinase Inhibitors [MoA]"));
            Assert.AreEqual("EP", AeDashboardDerivation.NormalizePharmacologicClassTypeFilter("[ep]"));
            Assert.AreEqual("Other", AeDashboardDerivation.ExtractPharmacologicClassType("Unbucketed Pharmacologic Class"));
            Assert.IsNull(AeDashboardDerivation.NormalizePharmacologicClassTypeFilter("All"));
            Assert.IsNull(AeDashboardDerivation.NormalizePharmacologicClassTypeFilter("*"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that system-scoped class maps correlate shared selected-SOC term profiles.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeSystemCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeSystemCorrelationMapAsync_PerfectlyCorrelatedClasses_ReturnsCoefficientOne()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string soc = "Cardiac Disorders";
            var terms = new[] { "Tachycardia", "Palpitations", "Chest pain" };
            var rrs = new[] { 2.0, 4.0, 8.0 };
            var riskId = 1;
            for (var i = 0; i < terms.Length; i++)
            {
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 100 + i, "CLASS-A", soc, rrs[i], parameterName: terms[i]);
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 200 + i, "CLASS-B", soc, rrs[i], parameterName: terms[i]);
            }

            var map = await DtoLabelAccess.GetAeSystemCorrelationMapAsync(context, new[] { soc.ToLowerInvariant() }, PkSecret, logger, minTermsPerCell: 3);

            Assert.IsNotNull(map);
            Assert.AreEqual(soc, map.SelectedSystems.Single());
            Assert.AreEqual(2, map.ClassCount);
            Assert.AreEqual(2, map.Classes.Count);
            Assert.IsTrue(map.Classes.All(axis => !string.IsNullOrWhiteSpace(axis.EncryptedPharmacologicClassID)));
            var offDiagonal = map.Cells.Single(cell => !cell.IsDiagonal);
            Assert.AreEqual(3, offDiagonal.PairCount);
            Assert.IsFalse(offDiagonal.InsufficientN);
            Assert.IsNotNull(offDiagonal.Coefficient);
            Assert.AreEqual(1.0, offDiagonal.Coefficient!.Value, 1e-9);
            Assert.IsFalse(map.IncludesFullMatrix);
            Assert.AreEqual(1, map.ClassPage.PageNumber);
            Assert.AreEqual(20, map.ClassPage.PageSize);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the selected class type filters system-scoped map axes while preserving facets.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeSystemCorrelationMapAsync"/>
        /// <seealso cref="AeSystemClassTypeFacetDto"/>
        [TestMethod]
        public async Task GetAeSystemCorrelationMapAsync_ClassTypeFilter_ReturnsFilteredAxisAndUnfilteredFacets()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string soc = "Cardiac Disorders";
            var terms = new[] { "Tachycardia", "Palpitations", "Chest pain" };
            var riskId = 1;
            foreach (var term in terms)
            {
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 101, "EPC-A", soc, 2.0 + riskId, pharmacologicClassId: 1001, parameterName: term, pharmClassName: "Kinase Inhibitor [EPC]");
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 102, "EPC-B", soc, 3.0 + riskId, pharmacologicClassId: 1002, parameterName: term, pharmClassName: "Protein Kinase Inhibitor [EPC]");
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 201, "MOA-A", soc, 4.0 + riskId, pharmacologicClassId: 2001, parameterName: term, pharmClassName: "Protein Kinase Inhibitors [MoA]");
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 202, "MOA-B", soc, 5.0 + riskId, pharmacologicClassId: 2002, parameterName: term, pharmClassName: "Tyrosine Kinase Inhibitors [MoA]");
            }

            var map = await DtoLabelAccess.GetAeSystemCorrelationMapAsync(
                context,
                new[] { soc },
                PkSecret,
                logger,
                minTermsPerCell: 3,
                classType: "[epc]");
            var fullMap = await DtoLabelAccess.GetAeSystemCorrelationMapAsync(
                context,
                new[] { soc },
                PkSecret,
                logger,
                minTermsPerCell: 3,
                includeFullMatrix: true,
                classType: "MOA");

            Assert.IsNotNull(map);
            Assert.AreEqual("EPC", map.SelectedClassType);
            Assert.AreEqual(2, map.ClassCount);
            Assert.AreEqual(2, map.ClassPage.TotalCount);
            Assert.IsTrue(map.Classes.All(axis => axis.ClassType == "EPC"));
            Assert.IsTrue(map.ClassSummaries.All(summary => summary.ClassType == "EPC"));
            Assert.AreEqual(2, map.ClassTypeFacets.Single(facet => facet.ClassType == "EPC").ClassCount);
            Assert.AreEqual(2, map.ClassTypeFacets.Single(facet => facet.ClassType == "MOA").ClassCount);
            Assert.IsTrue(map.ClassTypeFacets.Single(facet => facet.ClassType == "MOA").HasRenderableMap);

            Assert.IsNotNull(fullMap);
            Assert.IsTrue(fullMap.IncludesFullMatrix);
            Assert.AreEqual("MOA", fullMap.SelectedClassType);
            Assert.AreEqual(2, fullMap.ClassPage.TotalCount);
            Assert.IsTrue(fullMap.Classes.All(axis => axis.ClassType == "MOA"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the selected class type filters system-scoped heatmap class rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeSystemCorrelationHeatmapAsync"/>
        /// <seealso cref="AeSystemClassHeatmapDto"/>
        [TestMethod]
        public async Task GetAeSystemCorrelationHeatmapAsync_ClassTypeFilter_ReturnsFilteredClassRows()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string soc = "Cardiac Disorders";
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: "EPC-A", parameterCategory: soc, rr: 2.0, parameterName: "Tachycardia", pharmClassName: "Kinase Inhibitor [EPC]");
            seedCorrelationRow(connection, drugDoc(2), riskId: 2, activeMoietyId: 102, pharmClassCode: "MOA-A", parameterCategory: soc, rr: 3.0, parameterName: "Tachycardia", pharmClassName: "Protein Kinase Inhibitors [MoA]");
            seedCorrelationRow(connection, drugDoc(3), riskId: 3, activeMoietyId: 102, pharmClassCode: "MOA-A", parameterCategory: soc, rr: 4.0, parameterName: "Palpitations", pharmClassName: "Protein Kinase Inhibitors [MoA]");

            var heatmap = await DtoLabelAccess.GetAeSystemCorrelationHeatmapAsync(
                context,
                new[] { soc },
                PkSecret,
                logger,
                classPageNumber: 1,
                classPageSize: 10,
                drugPageNumber: 1,
                drugPageSize: 10,
                classType: "MOA");

            Assert.IsNotNull(heatmap);
            Assert.AreEqual("MOA", heatmap.SelectedClassType);
            Assert.AreEqual(1, heatmap.ClassPage.TotalCount);
            Assert.AreEqual(1, heatmap.Classes.Count);
            Assert.AreEqual("MOA", heatmap.Classes.Single().ClassType);
            Assert.IsTrue(heatmap.Cells.Count > 0);
            Assert.IsTrue(heatmap.ClassTypeFacets.Any(facet => facet.ClassType == "EPC"));
            Assert.IsTrue(heatmap.ClassTypeFacets.Any(facet => facet.ClassType == "MOA"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that full-matrix mode ignores the class page window and returns every filtered class pair.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeSystemCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeSystemCorrelationMapAsync_IncludeFullMatrix_ReturnsAllFilteredClassPairs()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string soc = "Cardiac Disorders";
            var terms = new[] { "Tachycardia", "Palpitations", "Chest pain" };
            var riskId = 1;
            foreach (var term in terms)
            {
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 100 + riskId, "CLASS-A", soc, 2.0 + riskId, parameterName: term);
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 200 + riskId, "CLASS-B", soc, 3.0 + riskId, parameterName: term);
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 300 + riskId, "CLASS-C", soc, 4.0 + riskId, parameterName: term);
            }

            var pagedMap = await DtoLabelAccess.GetAeSystemCorrelationMapAsync(
                context,
                new[] { soc },
                PkSecret,
                logger,
                classPageNumber: 1,
                classPageSize: 1,
                minTermsPerCell: 3);
            var fullMap = await DtoLabelAccess.GetAeSystemCorrelationMapAsync(
                context,
                new[] { soc },
                PkSecret,
                logger,
                classPageNumber: 1,
                classPageSize: 1,
                minTermsPerCell: 3,
                includeFullMatrix: true);

            Assert.IsNotNull(pagedMap);
            Assert.IsFalse(pagedMap.IncludesFullMatrix);
            Assert.AreEqual(3, pagedMap.ClassPage.TotalCount);
            Assert.AreEqual(1, pagedMap.Classes.Count);
            Assert.AreEqual(1, pagedMap.Cells.Count);
            Assert.IsTrue(pagedMap.Cells.Single().IsDiagonal);
            Assert.IsTrue(pagedMap.ClassPage.HasNextPage);

            Assert.IsNotNull(fullMap);
            Assert.IsTrue(fullMap.IncludesFullMatrix);
            Assert.AreEqual(3, fullMap.ClassPage.TotalCount);
            Assert.AreEqual(3, fullMap.ClassPage.PageSize);
            Assert.AreEqual(3, fullMap.Classes.Count);
            Assert.AreEqual(6, fullMap.Cells.Count);
            Assert.AreEqual(3, fullMap.Cells.Count(cell => cell.IsDiagonal));
            Assert.AreEqual(3, fullMap.Cells.Count(cell => !cell.IsDiagonal));
            Assert.IsFalse(fullMap.ClassPage.HasNextPage);
            Assert.IsFalse(fullMap.Warnings.Any(warning => warning.Contains("paging can hide", StringComparison.OrdinalIgnoreCase)));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that below-floor system-scoped class maps return an empty pruned matrix with warnings.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeSystemCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeSystemCorrelationMapAsync_BelowMinTermsPerCell_ReturnsEmptyPrunedMap()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string soc = "Cardiac Disorders";
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: "CLASS-A", parameterCategory: soc, rr: 2.0, parameterName: "Tachycardia");
            seedCorrelationRow(connection, drugDoc(2), riskId: 2, activeMoietyId: 102, pharmClassCode: "CLASS-B", parameterCategory: soc, rr: 2.0, parameterName: "Tachycardia");
            seedCorrelationRow(connection, drugDoc(3), riskId: 3, activeMoietyId: 103, pharmClassCode: "CLASS-A", parameterCategory: soc, rr: 4.0, parameterName: "Palpitations");
            seedCorrelationRow(connection, drugDoc(4), riskId: 4, activeMoietyId: 104, pharmClassCode: "CLASS-B", parameterCategory: soc, rr: 4.0, parameterName: "Palpitations");

            var map = await DtoLabelAccess.GetAeSystemCorrelationMapAsync(context, new[] { soc }, PkSecret, logger);

            Assert.IsNotNull(map);
            Assert.AreEqual(0, map.ClassCount);
            Assert.AreEqual(0, map.ClassPage.TotalCount);
            Assert.AreEqual(0, map.Classes.Count);
            Assert.AreEqual(0, map.Cells.Count);
            Assert.IsTrue(map.Warnings.Any(warning => warning.Contains("No off-diagonal", StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(map.Warnings.Any(warning => warning.Contains("Pruned", StringComparison.OrdinalIgnoreCase)));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that system-scoped class maps remove orphan classes before paging.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeSystemCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeSystemCorrelationMapAsync_OrphanClass_PrunesBeforePaging()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string soc = "Cardiac Disorders";
            var terms = new[] { "Tachycardia", "Palpitations", "Chest pain" };
            var riskId = 1;
            foreach (var term in terms)
            {
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 100 + riskId, "CLASS-A", soc, 2.0 + riskId, parameterName: term);
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 200 + riskId, "CLASS-B", soc, 3.0 + riskId, parameterName: term);
            }

            seedCorrelationRow(connection, drugDoc(riskId), riskId++, 300 + riskId, "CLASS-C", soc, 5.0, parameterName: "Unique orphan term");

            var map = await DtoLabelAccess.GetAeSystemCorrelationMapAsync(context, new[] { soc }, PkSecret, logger, minTermsPerCell: 3);
            var fullMap = await DtoLabelAccess.GetAeSystemCorrelationMapAsync(
                context,
                new[] { soc },
                PkSecret,
                logger,
                minTermsPerCell: 3,
                includeFullMatrix: true);

            Assert.IsNotNull(map);
            Assert.AreEqual(2, map.ClassCount);
            Assert.AreEqual(2, map.ClassPage.TotalCount);
            Assert.IsFalse(map.Classes.Any(axis => axis.PharmClassCode == "CLASS-C"));
            Assert.IsTrue(map.Warnings.Any(warning => warning.Contains("Pruned 1", StringComparison.OrdinalIgnoreCase)));

            Assert.IsNotNull(fullMap);
            Assert.IsTrue(fullMap.IncludesFullMatrix);
            Assert.AreEqual(2, fullMap.ClassPage.TotalCount);
            Assert.AreEqual(2, fullMap.ClassPage.PageSize);
            Assert.IsFalse(fullMap.Classes.Any(axis => axis.PharmClassCode == "CLASS-C"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that later class-axis pages compact around a renderable off-diagonal pair.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeSystemCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeSystemCorrelationMapAsync_PageWithoutRenderablePair_CompactsAroundPair()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string soc = "Cardiac Disorders";
            var firstPairTerms = new[] { "Term A1", "Term A2", "Term A3" };
            var secondPairTerms = new[] { "Term B1", "Term B2", "Term B3" };
            var riskId = 1;
            foreach (var term in firstPairTerms)
            {
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 100 + riskId, "CLASS-A", soc, 2.0 + riskId, parameterName: term);
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 200 + riskId, "CLASS-C", soc, 3.0 + riskId, parameterName: term);
            }

            foreach (var term in secondPairTerms)
            {
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 300 + riskId, "CLASS-B", soc, 4.0 + riskId, parameterName: term);
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 400 + riskId, "CLASS-D", soc, 5.0 + riskId, parameterName: term);
            }

            var map = await DtoLabelAccess.GetAeSystemCorrelationMapAsync(
                context,
                new[] { soc },
                PkSecret,
                logger,
                classPageNumber: 1,
                classPageSize: 2,
                minTermsPerCell: 3);

            Assert.IsNotNull(map);
            Assert.AreEqual(4, map.ClassCount);
            Assert.AreEqual(2, map.Classes.Count);
            Assert.IsTrue(map.Cells.Any(cell => !cell.IsDiagonal && cell.Coefficient.HasValue));
            Assert.IsTrue(map.Warnings.Any(warning => warning.Contains("compacted", StringComparison.OrdinalIgnoreCase)));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the system-scoped heatmap builds a sparse class x drug grid with paging metadata.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeSystemCorrelationHeatmapAsync"/>
        [TestMethod]
        public async Task GetAeSystemCorrelationHeatmapAsync_BuildsClassByDrugSparseGrid()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string soc = "Cardiac Disorders";
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: "CLASS-A", parameterCategory: soc, rr: 2.0, parameterName: "Tachycardia");
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: "CLASS-A", parameterCategory: soc, rr: 4.0, parameterName: "Palpitations");
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: "CLASS-B", parameterCategory: soc, rr: 8.0, parameterName: "Tachycardia");

            var heatmap = await DtoLabelAccess.GetAeSystemCorrelationHeatmapAsync(
                context,
                new[] { soc },
                PkSecret,
                logger,
                classPageNumber: 1,
                classPageSize: 1,
                drugPageNumber: 1,
                drugPageSize: 2);

            Assert.IsNotNull(heatmap);
            Assert.AreEqual(1, heatmap.Classes.Count);
            Assert.AreEqual(2, heatmap.ClassPage.TotalCount);
            Assert.IsTrue(heatmap.ClassPage.HasNextPage);
            Assert.AreEqual(2, heatmap.Drugs.Count);
            Assert.IsTrue(heatmap.Cells.Count > 0);
            Assert.IsTrue(heatmap.Cells.All(cell => cell.LogRr.HasValue && cell.Rr.HasValue && cell.Precision.HasValue));
            Assert.IsTrue(heatmap.Cells.Any(cell => cell.TermCount == 2));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that system-scoped cell detail returns shared term pairs and map-safe/raw coefficients.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeSystemCorrelationCellDetailAsync"/>
        [TestMethod]
        public async Task GetAeSystemCorrelationCellDetailAsync_ReturnsPerTermPairs()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string soc = "Cardiac Disorders";
            var terms = new[] { "Tachycardia", "Palpitations", "Chest pain" };
            var riskId = 1;
            foreach (var term in terms)
            {
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 100 + riskId, "CLASS-A", soc, 2.0 + riskId, parameterName: term);
                seedCorrelationRow(connection, drugDoc(riskId), riskId++, 200 + riskId, "CLASS-B", soc, 2.0 + riskId, parameterName: term);
            }

            var detail = await DtoLabelAccess.GetAeSystemCorrelationCellDetailAsync(context, new[] { soc }, "CLASS-A", "CLASS-B", PkSecret, logger, minTermsPerCell: 3);
            var diagonal = await DtoLabelAccess.GetAeSystemCorrelationCellDetailAsync(context, new[] { soc }, "CLASS-A", "class-a", PkSecret, logger, minTermsPerCell: 3);
            var missing = await DtoLabelAccess.GetAeSystemCorrelationCellDetailAsync(context, new[] { soc }, "CLASS-A", "NOPE", PkSecret, logger, minTermsPerCell: 3);

            Assert.IsNotNull(detail);
            Assert.AreEqual(3, detail.PairCount);
            Assert.AreEqual(3, detail.TermPairs.Count);
            Assert.IsNotNull(detail.Coefficient);
            Assert.IsNotNull(detail.RawCoefficient);
            Assert.IsTrue(detail.TermPairs.All(pair => pair.SystemOrganClass == soc && pair.LogRrX.HasValue && pair.LogRrY.HasValue));
            Assert.IsNotNull(diagonal);
            Assert.IsTrue(diagonal.IsDiagonal);
            Assert.AreEqual(1.0, diagonal.Coefficient);
            Assert.IsNotNull(missing);
            Assert.AreEqual(0, missing.TermPairs.Count);
            Assert.IsTrue(missing.Warnings.Any(warning => warning.Contains("no rows")));

            #endregion
        }

        #endregion system correlation tests
    }
}
