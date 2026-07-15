using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using MedRecProTest.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Integration.Persistence
{
    /**************************************************************/
    /// <summary>
    /// Establishes relational query-count evidence for the AE catalog paging baseline.
    /// </summary>
    /// <remarks>
    /// The test uses SQLite and a command interceptor because mocked DbSets and
    /// EF InMemory cannot prove provider-side filtering or bounded paging.
    /// </remarks>
    /// <seealso cref="CountingDbCommandInterceptor"/>
    /// <seealso cref="DtoLabelAccess.GetAeDrugSummariesAsync"/>
    [TestClass]
    [TestCategory("Integration")]
    public class DtoLabelAccessQueryCountBaselineTests
    {
        /**************************************************************/
        /// <summary>
        /// Verifies materialized catalog search and paging stay provider-side and bounded.
        /// </summary>
        /// <seealso cref="LabelView.AeDashboardProductCatalog"/>
        [TestMethod]
        public async Task GetAeDrugSummariesAsync_MaterializedCatalogSearchAndPaging_UsesBoundedRelationalCommands()
        {
            #region implementation

            DtoLabelAccessTestHelper.ClearCache();
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var schemaContext = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var interceptor = new CountingDbCommandInterceptor();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(interceptor)
                .Options;
            using var context = new ApplicationDbContext(options);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedAeDashboardProductCatalogTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "IBUPROFEN A");
            DtoLabelAccessTestHelper.SeedAeDashboardProductCatalogTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, "IBUPROFEN B");
            DtoLabelAccessTestHelper.SeedAeDashboardProductCatalogTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid3, "ASPIRIN C");

            var results = await DtoLabelAccess.GetAeDrugSummariesAsync(
                context,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger,
                productSearch: "ibuprofen",
                page: 1,
                size: 1);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("IBUPROFEN A", results.Single().ProductName);
            Assert.IsTrue(interceptor.Commands.Count <= 2,
                $"Expected catalog readiness plus one bounded query, but saw {interceptor.Commands.Count} commands.");
            Assert.IsTrue(interceptor.Commands.Any(command => command.Contains("WHERE", StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(interceptor.Commands.Any(command => command.Contains("LIMIT", StringComparison.OrdinalIgnoreCase)));

            #endregion
        }
    }
}
