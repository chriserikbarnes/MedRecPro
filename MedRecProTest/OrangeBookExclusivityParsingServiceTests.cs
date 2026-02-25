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
    /// Unit tests for <see cref="OrangeBookExclusivityParsingService"/>.
    /// </summary>
    /// <remarks>
    /// Tests cover line parsing, date parsing, full pipeline upsert,
    /// product FK linking, result count tracking, and edge cases
    /// (malformed rows, empty files, trailing blank lines).
    /// </remarks>
    /// <seealso cref="OrangeBookExclusivityParsingService"/>
    /// <seealso cref="OrangeBook.Exclusivity"/>
    /// <seealso cref="OrangeBookImportResult"/>
    [TestClass]
    public class OrangeBookExclusivityParsingServiceTests
    {
        #region Test Constants

        /// <summary>
        /// Header row for exclusivity.txt (5 tilde-delimited columns).
        /// </summary>
        private const string ExclusivityHeader = "Appl_Type~Appl_No~Product_No~Exclusivity_Code~Exclusivity_Date";

        /// <summary>
        /// Valid data row: NDA 020610, product 001, NCE exclusivity expiring Jul 13, 2026.
        /// </summary>
        private const string ValidRow1 = "N~020610~001~NCE~Jul 13, 2026";

        /// <summary>
        /// Valid data row: NDA 018680, product 002, PED exclusivity expiring Jun 28, 2027.
        /// </summary>
        private const string ValidRow2 = "N~018680~002~PED~Jun 28, 2027";

        /// <summary>
        /// Valid data row: ANDA 021825, product 001, ODE-417 exclusivity expiring Oct 10, 2028.
        /// </summary>
        private const string ValidRow3 = "A~021825~001~ODE-417~Oct 10, 2028";

        /// <summary>
        /// Malformed row with only 3 columns (expected 5).
        /// </summary>
        private const string MalformedRow = "N~020610~001";

        #endregion

        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates an SQLite in-memory database context with the OrangeBook schema applied.
        /// The connection is kept open for the lifetime of the context so the in-memory
        /// database persists across operations.
        /// </summary>
        /// <param name="connection">The open SQLite connection (caller must dispose).</param>
        /// <returns>A configured <see cref="ApplicationDbContext"/> backed by SQLite in-memory.</returns>
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
        /// Creates a service instance and its backing SQLite context.
        /// Wires up the IServiceScopeFactory → IServiceScope → IServiceProvider → ApplicationDbContext
        /// mock chain so the service can resolve its own scoped context.
        /// </summary>
        /// <param name="context">The test database context to register for DI resolution.</param>
        /// <returns>The configured <see cref="OrangeBookExclusivityParsingService"/>.</returns>
        private OrangeBookExclusivityParsingService createService(ApplicationDbContext context)
        {
            #region implementation
            var scopeFactory = new Mock<IServiceScopeFactory>();
            var scope = new Mock<IServiceScope>();
            var serviceProvider = new Mock<IServiceProvider>();

            scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);
            scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);
            serviceProvider.Setup(x => x.GetService(typeof(ApplicationDbContext)))
                .Returns(context);

            var logger = new Mock<ILogger<OrangeBookExclusivityParsingService>>();

            return new OrangeBookExclusivityParsingService(scopeFactory.Object, logger.Object);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a file content string from a header and data rows.
        /// </summary>
        /// <param name="rows">Data rows to include after the header.</param>
        /// <returns>Complete file content with header and newline-separated rows.</returns>
        private string buildFileContent(params string[] rows)
        {
            #region implementation
            var lines = new List<string> { ExclusivityHeader };
            lines.AddRange(rows);
            return string.Join("\n", lines);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds an OrangeBook.Product record in the test database for FK resolution tests.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="applType">Application type (e.g., "N").</param>
        /// <param name="applNo">Application number (e.g., "020610").</param>
        /// <param name="productNo">Product number (e.g., "001").</param>
        /// <returns>The seeded product's OrangeBookProductID.</returns>
        private async Task<int> seedProduct(ApplicationDbContext context, string applType, string applNo, string productNo)
        {
            #region implementation
            var product = new OrangeBook.Product
            {
                ApplType = applType,
                ApplNo = applNo,
                ProductNo = productNo,
                Ingredient = "TEST INGREDIENT"
            };
            context.Set<OrangeBook.Product>().Add(product);
            await context.SaveChangesAsync();
            return product.OrangeBookProductID!.Value;
            #endregion
        }

        #endregion

        #region Diagnostic

        /**************************************************************/
        /// <summary>
        /// Creates a shared named in-memory SQLite database that survives connection close/reopen.
        /// The service's finally block calls connection.CloseAsync(), which destroys a regular
        /// DataSource=:memory: database. A shared named DB + sentinel connection keeps it alive.
        /// </summary>
        /// <returns>
        /// A tuple of (sentinelConnection, serviceConnection, dbConnectionString).
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

        #region Test Methods — ParseLines

        /**************************************************************/
        /// <summary>
        /// Verifies that parseLines correctly skips the header row and returns
        /// all valid data rows.
        /// </summary>
        /// <seealso cref="OrangeBookExclusivityParsingService.parseLines"/>
        [TestMethod]
        public void ParseLines_ValidFile_ReturnsCorrectRowCount()
        {
            #region implementation
            // Arrange
            var service = createService(null!); // parseLines doesn't use the context
            var result = new OrangeBookImportResult();
            var fileContent = buildFileContent(ValidRow1, ValidRow2, ValidRow3);

            // Act
            var rows = service.parseLines(fileContent, result);

            // Assert
            Assert.AreEqual(3, rows.Count, "Should return 3 data rows after skipping header");
            Assert.AreEqual(0, result.MalformedRowsSkipped, "No malformed rows should be skipped");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that rows with incorrect column count are skipped and
        /// the malformed counter is incremented.
        /// </summary>
        /// <seealso cref="OrangeBookExclusivityParsingService.parseLines"/>
        [TestMethod]
        public void ParseLines_MalformedRow_SkipsAndIncrementsMalformedCount()
        {
            #region implementation
            // Arrange
            var service = createService(null!);
            var result = new OrangeBookImportResult();
            var fileContent = buildFileContent(ValidRow1, MalformedRow, ValidRow2);

            // Act
            var rows = service.parseLines(fileContent, result);

            // Assert
            Assert.AreEqual(2, rows.Count, "Should return 2 valid rows, skipping the malformed one");
            Assert.AreEqual(1, result.MalformedRowsSkipped, "Should increment malformed counter for the bad row");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a header-only file returns an empty list.
        /// </summary>
        /// <seealso cref="OrangeBookExclusivityParsingService.parseLines"/>
        [TestMethod]
        public void ParseLines_EmptyFile_ReturnsEmptyList()
        {
            #region implementation
            // Arrange
            var service = createService(null!);
            var result = new OrangeBookImportResult();
            var fileContent = ExclusivityHeader;

            // Act
            var rows = service.parseLines(fileContent, result);

            // Assert
            Assert.AreEqual(0, rows.Count, "Header-only file should return zero data rows");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that trailing empty lines at EOF are ignored.
        /// </summary>
        /// <seealso cref="OrangeBookExclusivityParsingService.parseLines"/>
        [TestMethod]
        public void ParseLines_TrailingEmptyLines_Ignored()
        {
            #region implementation
            // Arrange
            var service = createService(null!);
            var result = new OrangeBookImportResult();
            var fileContent = buildFileContent(ValidRow1) + "\n\n\n";

            // Act
            var rows = service.parseLines(fileContent, result);

            // Assert
            Assert.AreEqual(1, rows.Count, "Trailing empty lines should not produce extra rows");
            Assert.AreEqual(0, result.MalformedRowsSkipped, "Empty lines should not count as malformed");
            #endregion
        }

        #endregion

        #region Test Methods — ParseDate

        /**************************************************************/
        /// <summary>
        /// Verifies standard "MMM dd, yyyy" date format is parsed correctly.
        /// </summary>
        /// <seealso cref="OrangeBookExclusivityParsingService.parseDate"/>
        [TestMethod]
        public void ParseDate_StandardFormat_ReturnsParsedDate()
        {
            #region implementation
            // Arrange
            var service = createService(null!);

            // Act
            var date = service.parseDate("Apr 12, 2023");

            // Assert
            Assert.IsNotNull(date, "Standard date format should parse successfully");
            Assert.AreEqual(new DateTime(2023, 4, 12), date.Value, "Should parse to April 12, 2023");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies single-digit day "MMM d, yyyy" format is parsed correctly.
        /// </summary>
        /// <seealso cref="OrangeBookExclusivityParsingService.parseDate"/>
        [TestMethod]
        public void ParseDate_SingleDigitDay_ReturnsParsedDate()
        {
            #region implementation
            // Arrange
            var service = createService(null!);

            // Act
            var date = service.parseDate("Jul 1, 2026");

            // Assert
            Assert.IsNotNull(date, "Single-digit day format should parse successfully");
            Assert.AreEqual(new DateTime(2026, 7, 1), date.Value, "Should parse to July 1, 2026");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty string returns null.
        /// </summary>
        /// <seealso cref="OrangeBookExclusivityParsingService.parseDate"/>
        [TestMethod]
        public void ParseDate_EmptyString_ReturnsNull()
        {
            #region implementation
            // Arrange
            var service = createService(null!);

            // Act
            var date = service.parseDate("");

            // Assert
            Assert.IsNull(date, "Empty string should return null");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that unparseable text returns null.
        /// </summary>
        /// <seealso cref="OrangeBookExclusivityParsingService.parseDate"/>
        [TestMethod]
        public void ParseDate_InvalidText_ReturnsNull()
        {
            #region implementation
            // Arrange
            var service = createService(null!);

            // Act
            var date = service.parseDate("not a date");

            // Assert
            Assert.IsNull(date, "Invalid text should return null");
            #endregion
        }

        #endregion

        #region Test Methods — ProcessExclusivityFileAsync

        /**************************************************************/
        /// <summary>
        /// Verifies that new exclusivity records are inserted with correct field mapping.
        /// </summary>
        /// <seealso cref="OrangeBookExclusivityParsingService.ProcessExclusivityFileAsync"/>
        [TestMethod]
        public async Task ProcessExclusivityFileAsync_NewRecords_InsertsAll()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            var service = createService(context);
            var result = new OrangeBookImportResult();
            var fileContent = buildFileContent(ValidRow1, ValidRow2);

            // Act
            result = await service.ProcessExclusivityFileAsync(fileContent, result, CancellationToken.None);

            // Assert — reopen connection (service closes it in finally block; shared DB survives)
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
            var records = await context.Set<OrangeBook.Exclusivity>().ToListAsync();
            Assert.AreEqual(2, records.Count, "Should insert 2 exclusivity records");
            Assert.AreEqual(2, result.ExclusivityCreated, "ExclusivityCreated should be 2");
            Assert.AreEqual(0, result.ExclusivityUpdated, "ExclusivityUpdated should be 0 for first import");

            var nce = records.First(r => r.ExclusivityCode == "NCE");
            Assert.AreEqual("N", nce.ApplType, "ApplType should be 'N'");
            Assert.AreEqual("020610", nce.ApplNo, "ApplNo should be '020610'");
            Assert.AreEqual("001", nce.ProductNo, "ProductNo should be '001'");
            Assert.AreEqual(new DateTime(2026, 7, 13), nce.ExclusivityDate, "ExclusivityDate should be Jul 13, 2026");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that re-importing updates the ExclusivityDate on existing records.
        /// </summary>
        /// <seealso cref="OrangeBookExclusivityParsingService.ProcessExclusivityFileAsync"/>
        [TestMethod]
        public async Task ProcessExclusivityFileAsync_ExistingRecords_UpdatesDate()
        {
            #region implementation
            // Arrange — first import
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            var service = createService(context);
            var result1 = new OrangeBookImportResult();
            var fileContent1 = buildFileContent(ValidRow1);
            await service.ProcessExclusivityFileAsync(fileContent1, result1, CancellationToken.None);

            // Arrange — re-import with updated date
            var updatedRow = "N~020610~001~NCE~Dec 25, 2030";
            var fileContent2 = buildFileContent(updatedRow);
            var result2 = new OrangeBookImportResult();

            // Need a fresh service/context pointing to same DB since change tracker was cleared
            // Reopen connection first (service closes it in finally block)
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
            using var context2 = createTestContext(connection);
            var service2 = createService(context2);

            // Act
            await service2.ProcessExclusivityFileAsync(fileContent2, result2, CancellationToken.None);

            // Assert — reopen connection (service closes it in finally block; shared DB survives)
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
            var records = await context2.Set<OrangeBook.Exclusivity>().ToListAsync();
            Assert.AreEqual(1, records.Count, "Should still have 1 record (updated, not duplicated)");
            Assert.AreEqual(0, result2.ExclusivityCreated, "No new records created on re-import");
            Assert.AreEqual(1, result2.ExclusivityUpdated, "One record should be updated");
            Assert.AreEqual(new DateTime(2030, 12, 25), records[0].ExclusivityDate, "Date should be updated to Dec 25, 2030");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that exclusivity records are linked to seeded OrangeBook.Product
        /// records when the composite key matches.
        /// </summary>
        /// <seealso cref="OrangeBookExclusivityParsingService.ProcessExclusivityFileAsync"/>
        [TestMethod]
        public async Task ProcessExclusivityFileAsync_WithProductMatch_SetsProductId()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            var productId = await seedProduct(context, "N", "020610", "001");
            var service = createService(context);
            var result = new OrangeBookImportResult();
            var fileContent = buildFileContent(ValidRow1);

            // Act
            result = await service.ProcessExclusivityFileAsync(fileContent, result, CancellationToken.None);

            // Assert — reopen connection (service closes it in finally block; shared DB survives)
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
            var records = await context.Set<OrangeBook.Exclusivity>().ToListAsync();
            Assert.AreEqual(1, records.Count, "Should insert 1 exclusivity record");
            Assert.AreEqual(productId, records[0].OrangeBookProductID, "Should be linked to the seeded product");
            Assert.AreEqual(1, result.ExclusivityLinkedToProduct, "Should count 1 linked exclusivity");
            Assert.AreEqual(0, result.UnlinkedExclusivity, "Should have 0 unlinked exclusivity");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that exclusivity records without a matching product have null
        /// OrangeBookProductID and are counted as unlinked.
        /// </summary>
        /// <seealso cref="OrangeBookExclusivityParsingService.ProcessExclusivityFileAsync"/>
        [TestMethod]
        public async Task ProcessExclusivityFileAsync_NoProductMatch_SetsNullProductId()
        {
            #region implementation
            // Arrange — no products seeded
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            var service = createService(context);
            var result = new OrangeBookImportResult();
            var fileContent = buildFileContent(ValidRow1);

            // Act
            result = await service.ProcessExclusivityFileAsync(fileContent, result, CancellationToken.None);

            // Assert — reopen connection (service closes it in finally block; shared DB survives)
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
            var records = await context.Set<OrangeBook.Exclusivity>().ToListAsync();
            Assert.AreEqual(1, records.Count, "Should insert 1 exclusivity record");
            Assert.IsNull(records[0].OrangeBookProductID, "Should have null OrangeBookProductID when no product match");
            Assert.AreEqual(0, result.ExclusivityLinkedToProduct, "Should have 0 linked exclusivity");
            Assert.AreEqual(1, result.UnlinkedExclusivity, "Should count 1 unlinked exclusivity");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the result object correctly tracks all count fields
        /// across a mixed import with both linked and unlinked records.
        /// </summary>
        /// <seealso cref="OrangeBookExclusivityParsingService.ProcessExclusivityFileAsync"/>
        [TestMethod]
        public async Task ProcessExclusivityFileAsync_TracksResultCounts()
        {
            #region implementation
            // Arrange — seed one product that matches ValidRow1 but not ValidRow2
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            await seedProduct(context, "N", "020610", "001");
            var service = createService(context);
            var result = new OrangeBookImportResult();
            var fileContent = buildFileContent(ValidRow1, ValidRow2, ValidRow3);

            // Act
            result = await service.ProcessExclusivityFileAsync(fileContent, result, CancellationToken.None);

            // Assert
            Assert.AreEqual(3, result.ExclusivityCreated, "Should create 3 records");
            Assert.AreEqual(0, result.ExclusivityUpdated, "No updates on first import");
            Assert.AreEqual(1, result.ExclusivityLinkedToProduct, "Only ValidRow1 matches the seeded product");
            Assert.AreEqual(2, result.UnlinkedExclusivity, "ValidRow2 and ValidRow3 have no matching products");
            Assert.IsTrue(result.Success, "Import should succeed with no errors");
            #endregion
        }

        #endregion
    }
}
