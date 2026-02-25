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
    /// Unit tests for <see cref="OrangeBookPatentParsingService"/>.
    /// </summary>
    /// <remarks>
    /// Tests cover line parsing (10-column validation), date parsing,
    /// Y-flag boolean parsing, exception message chain walking,
    /// full pipeline upsert with FK linking, and result count tracking.
    /// </remarks>
    /// <seealso cref="OrangeBookPatentParsingService"/>
    /// <seealso cref="OrangeBook.Patent"/>
    /// <seealso cref="OrangeBookImportResult"/>
    [TestClass]
    public class OrangeBookPatentParsingServiceTests
    {
        #region Test Constants

        /// <summary>
        /// Header row for patent.txt (10 tilde-delimited columns).
        /// </summary>
        private const string PatentHeader = "Appl_Type~Appl_No~Product_No~Patent_No~Patent_Expire_Date_Text~Drug_Substance_Flag~Drug_Product_Flag~Patent_Use_Code~Delist_Flag~Submission_Date";

        /// <summary>
        /// Valid data row: NDA 020610, product 001, patent 7625884 with substance flag and use code.
        /// </summary>
        private const string ValidRow1 = "N~020610~001~7625884~Aug 24, 2026~Y~~U-141~~Jun 27, 2013";

        /// <summary>
        /// Valid data row: NDA 018613, product 002, patent 7560445 with product flag.
        /// </summary>
        private const string ValidRow2 = "N~018613~002~7560445~Feb 1, 2027~~Y~~~May 1, 2013";

        /// <summary>
        /// Valid data row: NDA 019734, product 005, patent with whitespace-only use code (should become null).
        /// </summary>
        private const string ValidRowWhitespaceUseCode = "N~019734~005~8455524~Apr 18, 2027~Y~~ ~~Sep 23, 2011";

        /// <summary>
        /// Malformed row with only 5 columns (expected 10).
        /// </summary>
        private const string MalformedRow = "N~020610~001~7625884~Aug 24, 2026";

        #endregion

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
        /// Creates a patent parsing service instance wired to the given context via mock DI.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <returns>The configured <see cref="OrangeBookPatentParsingService"/>.</returns>
        private OrangeBookPatentParsingService createService(ApplicationDbContext context)
        {
            #region implementation
            var scopeFactory = new Mock<IServiceScopeFactory>();
            var scope = new Mock<IServiceScope>();
            var serviceProvider = new Mock<IServiceProvider>();

            scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);
            scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);
            serviceProvider.Setup(x => x.GetService(typeof(ApplicationDbContext)))
                .Returns(context);

            var logger = new Mock<ILogger<OrangeBookPatentParsingService>>();

            return new OrangeBookPatentParsingService(scopeFactory.Object, logger.Object);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a file content string from header and data rows.
        /// </summary>
        /// <param name="rows">Data rows to include after the header.</param>
        /// <returns>Complete file content.</returns>
        private string buildFileContent(params string[] rows)
        {
            #region implementation
            var lines = new List<string> { PatentHeader };
            lines.AddRange(rows);
            return string.Join("\n", lines);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds an OrangeBook.Product record for FK resolution tests.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="applType">Application type.</param>
        /// <param name="applNo">Application number.</param>
        /// <param name="productNo">Product number.</param>
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

        [TestMethod]
        public void DiagnoseDdlFailures()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ApplicationDbContext(options);
            var ddl = context.Database.GenerateCreateScript();
            ddl = Regex.Replace(ddl, @"\b(n?varchar|nchar|varbinary)\s*\(\s*max\s*\)", "TEXT", RegexOptions.IgnoreCase);
            ddl = Regex.Replace(ddl, @"\bchar\(\d+\)", "TEXT", RegexOptions.IgnoreCase);
            ddl = Regex.Replace(ddl, @"\bdecimal\(\d+,\s*\d+\)", "REAL", RegexOptions.IgnoreCase);
            ddl = ddl.Replace("SYSUTCDATETIME()", "datetime('now')");

            var failures = new List<string>();
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
                catch (Microsoft.Data.Sqlite.SqliteException ex)
                {
                    var preview = trimmed.Length > 200 ? trimmed[..200] + "..." : trimmed;
                    failures.Add($"ERR: {ex.Message}\nDDL: {preview}\n");
                }
            }

            // List OrangeBook tables
            using var listCmd = connection.CreateCommand();
            listCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'OrangeBook%' ORDER BY name";
            using var reader = listCmd.ExecuteReader();
            var tables = new List<string>();
            while (reader.Read()) tables.Add(reader.GetString(0));

            if (failures.Count > 0)
                Assert.Fail($"DDL failures ({failures.Count}):\n{string.Join("\n", failures)}\n\nExisting OrangeBook tables: {string.Join(", ", tables)}");
        }

        #region Test Methods — ParseLines

        /**************************************************************/
        /// <summary>
        /// Verifies that parseLines correctly skips header and returns valid 10-column rows.
        /// </summary>
        /// <seealso cref="OrangeBookPatentParsingService.parseLines"/>
        [TestMethod]
        public void ParseLines_ValidFile_ReturnsCorrectRowCount()
        {
            #region implementation
            // Arrange
            var service = createService(null!);
            var result = new OrangeBookImportResult();
            var fileContent = buildFileContent(ValidRow1, ValidRow2);

            // Act
            var rows = service.parseLines(fileContent, result);

            // Assert
            Assert.AreEqual(2, rows.Count, "Should return 2 data rows after skipping header");
            Assert.AreEqual(0, result.MalformedRowsSkipped, "No malformed rows should be skipped");
            Assert.AreEqual(10, rows[0].Length, "Each row should have exactly 10 columns");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that rows with incorrect column count are skipped.
        /// </summary>
        /// <seealso cref="OrangeBookPatentParsingService.parseLines"/>
        [TestMethod]
        public void ParseLines_MalformedRow_SkipsRow()
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
            Assert.AreEqual(1, result.MalformedRowsSkipped, "Should count 1 malformed row");
            #endregion
        }

        #endregion

        #region Test Methods — ParseYFlag

        /**************************************************************/
        /// <summary>
        /// Verifies that uppercase "Y" returns true.
        /// </summary>
        /// <seealso cref="OrangeBookPatentParsingService.parseYFlag"/>
        [TestMethod]
        public void ParseYFlag_UpperY_ReturnsTrue()
        {
            #region implementation
            var service = createService(null!);
            Assert.IsTrue(service.parseYFlag("Y"), "Uppercase 'Y' should return true");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that lowercase "y" returns true (case-insensitive).
        /// </summary>
        /// <seealso cref="OrangeBookPatentParsingService.parseYFlag"/>
        [TestMethod]
        public void ParseYFlag_LowerY_ReturnsTrue()
        {
            #region implementation
            var service = createService(null!);
            Assert.IsTrue(service.parseYFlag("y"), "Lowercase 'y' should return true");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that empty string returns false.
        /// </summary>
        /// <seealso cref="OrangeBookPatentParsingService.parseYFlag"/>
        [TestMethod]
        public void ParseYFlag_Empty_ReturnsFalse()
        {
            #region implementation
            var service = createService(null!);
            Assert.IsFalse(service.parseYFlag(""), "Empty string should return false");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that non-Y values return false.
        /// </summary>
        /// <seealso cref="OrangeBookPatentParsingService.parseYFlag"/>
        [TestMethod]
        public void ParseYFlag_OtherValue_ReturnsFalse()
        {
            #region implementation
            var service = createService(null!);
            Assert.IsFalse(service.parseYFlag("N"), "'N' should return false");
            Assert.IsFalse(service.parseYFlag("X"), "'X' should return false");
            Assert.IsFalse(service.parseYFlag("Yes"), "'Yes' should return false (only 'Y' is accepted)");
            #endregion
        }

        #endregion

        #region Test Methods — GetFullExceptionMessage

        /**************************************************************/
        /// <summary>
        /// Verifies that a single exception returns its message.
        /// </summary>
        /// <seealso cref="OrangeBookPatentParsingService.getFullExceptionMessage"/>
        [TestMethod]
        public void GetFullExceptionMessage_SingleException_ReturnsMessage()
        {
            #region implementation
            // Arrange
            var ex = new InvalidOperationException("Something went wrong");

            // Act
            var message = OrangeBookPatentParsingService.getFullExceptionMessage(ex);

            // Assert
            Assert.AreEqual("Something went wrong", message, "Should return the single exception message");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that nested exceptions are chained with " → " separator.
        /// </summary>
        /// <seealso cref="OrangeBookPatentParsingService.getFullExceptionMessage"/>
        [TestMethod]
        public void GetFullExceptionMessage_NestedExceptions_ChainsWithArrow()
        {
            #region implementation
            // Arrange
            var inner = new ArgumentException("bad argument");
            var outer = new InvalidOperationException("operation failed", inner);

            // Act
            var message = OrangeBookPatentParsingService.getFullExceptionMessage(outer);

            // Assert
            Assert.IsTrue(message.Contains("operation failed"), "Should contain outer message");
            Assert.IsTrue(message.Contains("bad argument"), "Should contain inner message");
            Assert.IsTrue(message.Contains(" → "), "Should chain messages with ' → '");
            #endregion
        }

        #endregion

        #region Test Methods — ProcessPatentsFileAsync

        /**************************************************************/
        /// <summary>
        /// Verifies that new patent records are inserted with correct field mapping
        /// including boolean flags and dates.
        /// </summary>
        /// <seealso cref="OrangeBookPatentParsingService.ProcessPatentsFileAsync"/>
        [TestMethod]
        public async Task ProcessPatentsFileAsync_NewRecords_InsertsAll()
        {
            #region implementation
            // Arrange
            using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            using var context = createTestContext(connection);
            var service = createService(context);
            var result = new OrangeBookImportResult();
            var fileContent = buildFileContent(ValidRow1);

            // Act
            result = await service.ProcessPatentsFileAsync(fileContent, result, CancellationToken.None);

            // Assert
            var records = await context.Set<OrangeBook.Patent>().ToListAsync();
            Assert.AreEqual(1, records.Count, "Should insert 1 patent record");
            Assert.AreEqual(1, result.PatentsCreated, "PatentsCreated should be 1");

            var patent = records[0];
            Assert.AreEqual("N", patent.ApplType, "ApplType should be 'N'");
            Assert.AreEqual("020610", patent.ApplNo, "ApplNo should be '020610'");
            Assert.AreEqual("001", patent.ProductNo, "ProductNo should be '001'");
            Assert.AreEqual("7625884", patent.PatentNo, "PatentNo should be '7625884'");
            Assert.AreEqual(new DateTime(2026, 8, 24), patent.PatentExpireDate, "PatentExpireDate should be Aug 24, 2026");
            Assert.AreEqual(true, patent.DrugSubstanceFlag, "DrugSubstanceFlag should be true (source: 'Y')");
            Assert.AreEqual(false, patent.DrugProductFlag, "DrugProductFlag should be false (source: empty)");
            Assert.AreEqual("U-141", patent.PatentUseCode, "PatentUseCode should be 'U-141'");
            Assert.AreEqual(false, patent.DelistFlag, "DelistFlag should be false (source: empty)");
            Assert.AreEqual(new DateTime(2013, 6, 27), patent.SubmissionDate, "SubmissionDate should be Jun 27, 2013");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that re-importing updates fields on existing records.
        /// </summary>
        /// <seealso cref="OrangeBookPatentParsingService.ProcessPatentsFileAsync"/>
        [TestMethod]
        public async Task ProcessPatentsFileAsync_ExistingRecords_UpdatesFields()
        {
            #region implementation
            // Arrange — first import
            using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            using var context = createTestContext(connection);
            var service = createService(context);
            var result1 = new OrangeBookImportResult();
            await service.ProcessPatentsFileAsync(buildFileContent(ValidRow1), result1, CancellationToken.None);

            // Arrange — re-import with updated expire date
            var updatedRow = "N~020610~001~7625884~Dec 31, 2030~Y~~U-141~~Jun 27, 2013";
            using var context2 = createTestContext(connection);
            var service2 = createService(context2);
            var result2 = new OrangeBookImportResult();

            // Act
            await service2.ProcessPatentsFileAsync(buildFileContent(updatedRow), result2, CancellationToken.None);

            // Assert
            var records = await context2.Set<OrangeBook.Patent>().ToListAsync();
            Assert.AreEqual(1, records.Count, "Should still have 1 record (updated, not duplicated)");
            Assert.AreEqual(0, result2.PatentsCreated, "No new patents created");
            Assert.AreEqual(1, result2.PatentsUpdated, "One patent should be updated");
            Assert.AreEqual(new DateTime(2030, 12, 31), records[0].PatentExpireDate, "Expire date should be updated");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that patents are linked to seeded products.
        /// </summary>
        /// <seealso cref="OrangeBookPatentParsingService.ProcessPatentsFileAsync"/>
        [TestMethod]
        public async Task ProcessPatentsFileAsync_WithProductMatch_SetsProductId()
        {
            #region implementation
            // Arrange
            using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            using var context = createTestContext(connection);
            var productId = await seedProduct(context, "N", "020610", "001");
            var service = createService(context);
            var result = new OrangeBookImportResult();

            // Act
            result = await service.ProcessPatentsFileAsync(buildFileContent(ValidRow1), result, CancellationToken.None);

            // Assert
            var records = await context.Set<OrangeBook.Patent>().ToListAsync();
            Assert.AreEqual(productId, records[0].OrangeBookProductID, "Should link to the seeded product");
            Assert.AreEqual(1, result.PatentsLinkedToProduct, "Should count 1 linked patent");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that whitespace-only PatentUseCode is stored as null.
        /// </summary>
        /// <seealso cref="OrangeBookPatentParsingService.ProcessPatentsFileAsync"/>
        [TestMethod]
        public async Task ProcessPatentsFileAsync_NullPatentUseCode_StoredAsNull()
        {
            #region implementation
            // Arrange
            using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            using var context = createTestContext(connection);
            var service = createService(context);
            var result = new OrangeBookImportResult();

            // Act
            result = await service.ProcessPatentsFileAsync(buildFileContent(ValidRowWhitespaceUseCode), result, CancellationToken.None);

            // Assert
            var records = await context.Set<OrangeBook.Patent>().ToListAsync();
            Assert.AreEqual(1, records.Count, "Should insert 1 patent record");
            Assert.IsNull(records[0].PatentUseCode, "Whitespace-only PatentUseCode should be stored as null");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that result counts are tracked correctly across a multi-row import.
        /// </summary>
        /// <seealso cref="OrangeBookPatentParsingService.ProcessPatentsFileAsync"/>
        [TestMethod]
        public async Task ProcessPatentsFileAsync_TracksResultCounts()
        {
            #region implementation
            // Arrange — seed one matching product
            using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            using var context = createTestContext(connection);
            await seedProduct(context, "N", "020610", "001");
            var service = createService(context);
            var result = new OrangeBookImportResult();

            // Act — import 2 rows, only one matching a product
            result = await service.ProcessPatentsFileAsync(buildFileContent(ValidRow1, ValidRow2), result, CancellationToken.None);

            // Assert
            Assert.AreEqual(2, result.PatentsCreated, "Should create 2 patent records");
            Assert.AreEqual(0, result.PatentsUpdated, "No updates on first import");
            Assert.AreEqual(1, result.PatentsLinkedToProduct, "Only ValidRow1 matches the seeded product");
            Assert.AreEqual(1, result.UnlinkedPatents, "ValidRow2 has no matching product");
            #endregion
        }

        #endregion
    }
}
