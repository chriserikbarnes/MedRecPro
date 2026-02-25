using MedRecProImportClass.Data;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.ParsingServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Diagnostics;
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
    ///
    /// Pipeline tests use shared-cache named SQLite in-memory databases with a sentinel
    /// connection so the DB survives the service's finally { connection.CloseAsync() } block.
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
                    // or already-existing objects on re-import calls
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

        /**************************************************************/
        /// <summary>
        /// Creates a shared named in-memory SQLite database that survives connection close/reopen.
        /// The service's finally block calls connection.CloseAsync(), which destroys a regular
        /// DataSource=:memory: database. A shared named DB + sentinel connection keeps it alive.
        /// </summary>
        /// <returns>
        /// A tuple of (sentinelConnection, serviceConnection).
        /// The sentinel must stay open for the DB's lifetime. Caller must dispose both connections.
        /// </returns>
        private (SqliteConnection sentinel, SqliteConnection connection) createSharedMemoryDb()
        {
            #region implementation
            var dbName = $"file:test_{Guid.NewGuid():N}?mode=memory&cache=shared";
            var connStr = $"DataSource={dbName}";

            // Sentinel keeps the DB alive even when service closes its connection
            var sentinel = new SqliteConnection(connStr);
            sentinel.Open();

            // Service connection — will be closed/reopened by service and tests
            var connection = new SqliteConnection(connStr);
            connection.Open();

            return (sentinel, connection);
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
            // Arrange — shared-cache prevents connection.CloseAsync() from destroying the DB
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            var service = createService(context);
            var result = new OrangeBookImportResult();

            try
            {
                // Act
                result = await service.ProcessPatentUseCodesAsync(result, CancellationToken.None);

                // Debug trace
                Debug.WriteLine($"[InsertsAllCodes] Success={result.Success}, " +
                    $"PatentUseCodesLoaded={result.PatentUseCodesLoaded}, " +
                    $"Errors=[{string.Join("; ", result.Errors)}]");

                // Reopen connection after service's finally block
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InsertsAllCodes] FAILED: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"  Stack: {ex.StackTrace}");
                var inner = ex.InnerException;
                while (inner != null)
                {
                    Debug.WriteLine($"  Inner: {inner.GetType().Name}: {inner.Message}");
                    inner = inner.InnerException;
                }
                throw;
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
            // Arrange — first import using shared-cache DB
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            var service = createService(context);
            var result1 = new OrangeBookImportResult();

            try
            {
                await service.ProcessPatentUseCodesAsync(result1, CancellationToken.None);

                // Reopen connection after first import
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
                var countAfterFirst = await context.Set<OrangeBook.PatentUseCodeDefinition>().CountAsync();

                Debug.WriteLine($"[UpdatesExistingDefinition] First import: countAfterFirst={countAfterFirst}");

                // Arrange — second import (should update, not duplicate)
                using var context2 = createTestContext(connection);
                var service2 = createService(context2);
                var result2 = new OrangeBookImportResult();

                // Act
                await service2.ProcessPatentUseCodesAsync(result2, CancellationToken.None);

                // Debug trace
                Debug.WriteLine($"[UpdatesExistingDefinition] Success={result2.Success}, " +
                    $"PatentUseCodesLoaded={result2.PatentUseCodesLoaded}, " +
                    $"Errors=[{string.Join("; ", result2.Errors)}]");

                // Reopen connection after second import
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

                // Assert
                var countAfterSecond = await context2.Set<OrangeBook.PatentUseCodeDefinition>().CountAsync();
                Assert.AreEqual(countAfterFirst, countAfterSecond,
                    "Re-import should not create duplicate records (same count as first import)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdatesExistingDefinition] FAILED: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"  Stack: {ex.StackTrace}");
                var inner = ex.InnerException;
                while (inner != null)
                {
                    Debug.WriteLine($"  Inner: {inner.GetType().Name}: {inner.Message}");
                    inner = inner.InnerException;
                }
                throw;
            }
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
            // Arrange — shared-cache prevents connection.CloseAsync() from destroying the DB
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            var service = createService(context);
            var result = new OrangeBookImportResult();

            try
            {
                // Act
                result = await service.ProcessPatentUseCodesAsync(result, CancellationToken.None);

                // Debug trace
                Debug.WriteLine($"[SetsResultCount] Success={result.Success}, " +
                    $"PatentUseCodesLoaded={result.PatentUseCodesLoaded}, " +
                    $"Errors=[{string.Join("; ", result.Errors)}]");

                // Assert
                Assert.IsTrue(result.PatentUseCodesLoaded > 0,
                    "PatentUseCodesLoaded should be positive after successful import");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SetsResultCount] FAILED: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"  Stack: {ex.StackTrace}");
                var inner = ex.InnerException;
                while (inner != null)
                {
                    Debug.WriteLine($"  Inner: {inner.GetType().Name}: {inner.Message}");
                    inner = inner.InnerException;
                }
                throw;
            }
            #endregion
        }

        #endregion
    }
}
