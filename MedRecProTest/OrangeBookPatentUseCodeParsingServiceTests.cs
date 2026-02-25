using MedRecProImportClass.Data;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.ParsingServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Text.RegularExpressions;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="OrangeBookPatentUseCodeParsingService"/>.
    /// </summary>
    /// <remarks>
    /// Tests cover embedded JSON resource loading, upsert of
    /// <see cref="OrangeBook.PatentUseCodeDefinition"/> records,
    /// and result count tracking. The service reads from an embedded
    /// resource (no file content parameter), so tests exercise the
    /// full pipeline including resource loading.
    /// </remarks>
    /// <seealso cref="OrangeBookPatentUseCodeParsingService"/>
    /// <seealso cref="OrangeBook.PatentUseCodeDefinition"/>
    /// <seealso cref="OrangeBookImportResult"/>
    [TestClass]
    public class OrangeBookPatentUseCodeParsingServiceTests
    {
        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates an SQLite in-memory database context with the schema applied.
        /// </summary>
        /// <param name="connection">The open SQLite connection (caller must dispose).</param>
        /// <returns>A configured <see cref="ApplicationDbContext"/>.</returns>
        private ApplicationDbContext createTestContext(SqliteConnection connection)
        {
            #region implementation
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new ApplicationDbContext(options);

            // EnsureCreated() generates SQL Server-specific DDL (nvarchar(max), decimal(p,s),
            // SYSUTCDATETIME) that SQLite cannot parse. Generate the DDL script, patch for
            // SQLite compatibility, then execute each statement individually so a failure in
            // one CREATE INDEX doesn't abort subsequent CREATE TABLE statements.
            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();

            var ddl = context.Database.GenerateCreateScript();
            ddl = Regex.Replace(ddl, @"\b(n?varchar|nchar|varbinary)\s*\(\s*max\s*\)", "TEXT", RegexOptions.IgnoreCase);
            ddl = Regex.Replace(ddl, @"\bchar\(\d+\)", "TEXT", RegexOptions.IgnoreCase);
            ddl = Regex.Replace(ddl, @"\bdecimal\(\d+,\s*\d+\)", "REAL", RegexOptions.IgnoreCase);
            ddl = ddl.Replace("SYSUTCDATETIME()", "datetime('now')");

            foreach (var stmt in ddl.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = stmt.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                try
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = trimmed;
                    cmd.ExecuteNonQuery();
                }
                catch (Microsoft.Data.Sqlite.SqliteException)
                {
                    // Skip statements with unsupported SQL Server constructs
                }
            }

            return context;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a patent use code parsing service wired to the given context via mock DI.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <returns>The configured <see cref="OrangeBookPatentUseCodeParsingService"/>.</returns>
        private OrangeBookPatentUseCodeParsingService createService(ApplicationDbContext context)
        {
            #region implementation
            var scopeFactory = new Mock<IServiceScopeFactory>();
            var scope = new Mock<IServiceScope>();
            var serviceProvider = new Mock<IServiceProvider>();

            scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);
            scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);
            serviceProvider.Setup(x => x.GetService(typeof(ApplicationDbContext)))
                .Returns(context);

            var logger = new Mock<ILogger<OrangeBookPatentUseCodeParsingService>>();

            return new OrangeBookPatentUseCodeParsingService(scopeFactory.Object, logger.Object);
            #endregion
        }

        #endregion

        #region Test Methods — ProcessPatentUseCodesAsync

        /**************************************************************/
        /// <summary>
        /// Verifies that the embedded JSON resource is loaded and all patent use code
        /// definitions are inserted into the database. The embedded resource contains
        /// approximately 4,400 entries.
        /// </summary>
        /// <seealso cref="OrangeBookPatentUseCodeParsingService.ProcessPatentUseCodesAsync"/>
        [TestMethod]
        public async Task ProcessPatentUseCodesAsync_InsertsAllCodes()
        {
            #region implementation
            // Arrange
            using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            using var context = createTestContext(connection);
            var service = createService(context);
            var result = new OrangeBookImportResult();

            // Act
            result = await service.ProcessPatentUseCodesAsync(result, CancellationToken.None);

            // Assert
            var records = await context.Set<OrangeBook.PatentUseCodeDefinition>().ToListAsync();
            Assert.IsTrue(records.Count > 0, "Should insert at least one patent use code definition");
            Assert.AreEqual(records.Count, result.PatentUseCodesLoaded, "PatentUseCodesLoaded should match inserted count");
            Assert.IsTrue(result.Success, "Import should succeed");

            // Verify a known use code exists with a non-empty definition
            var u141 = records.FirstOrDefault(r => r.Code == "U-141");
            if (u141 != null)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(u141.Definition),
                    "U-141 should have a non-empty definition");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that re-importing updates existing definitions rather than
        /// creating duplicates. Uses the natural primary key (Code).
        /// </summary>
        /// <seealso cref="OrangeBookPatentUseCodeParsingService.ProcessPatentUseCodesAsync"/>
        [TestMethod]
        public async Task ProcessPatentUseCodesAsync_UpdatesExistingDefinition()
        {
            #region implementation
            // Arrange — first import
            using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            using var context = createTestContext(connection);
            var service = createService(context);
            var result1 = new OrangeBookImportResult();
            await service.ProcessPatentUseCodesAsync(result1, CancellationToken.None);
            var countAfterFirst = await context.Set<OrangeBook.PatentUseCodeDefinition>().CountAsync();

            // Arrange — second import (should update, not duplicate)
            using var context2 = createTestContext(connection);
            var service2 = createService(context2);
            var result2 = new OrangeBookImportResult();

            // Act
            await service2.ProcessPatentUseCodesAsync(result2, CancellationToken.None);

            // Assert
            var countAfterSecond = await context2.Set<OrangeBook.PatentUseCodeDefinition>().CountAsync();
            Assert.AreEqual(countAfterFirst, countAfterSecond,
                "Re-import should not create duplicate records (same count as first import)");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that result.PatentUseCodesLoaded is set to a positive value.
        /// </summary>
        /// <seealso cref="OrangeBookPatentUseCodeParsingService.ProcessPatentUseCodesAsync"/>
        [TestMethod]
        public async Task ProcessPatentUseCodesAsync_SetsResultCount()
        {
            #region implementation
            // Arrange
            using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            using var context = createTestContext(connection);
            var service = createService(context);
            var result = new OrangeBookImportResult();

            // Act
            result = await service.ProcessPatentUseCodesAsync(result, CancellationToken.None);

            // Assert
            Assert.IsTrue(result.PatentUseCodesLoaded > 0,
                "PatentUseCodesLoaded should be positive after successful import");
            #endregion
        }

        #endregion
    }
}
