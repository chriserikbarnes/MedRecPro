using MedRecProImportClass.Data;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Text.RegularExpressions;
using LabelContainer = MedRecProImportClass.Models.Label;
using LabelViewContainer = MedRecProImportClass.Models.LabelView;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="TableCellContextService"/> and <see cref="TableCellContextFilter"/>.
    /// </summary>
    /// <remarks>
    /// Tests cover:
    /// - Constructor guard clauses
    /// - Full LINQ join projection (all 26 DTO properties)
    /// - Filter by DocumentGUID, TextTableID, range, and MaxRows
    /// - Grouped-by-table result shape
    /// - TextTableID min/max range discovery
    /// - TableCellContextFilter validation rules
    ///
    /// Uses SQLite in-memory database with DDL patching following the pattern
    /// from <see cref="OrangeBookProductParsingServiceTests"/>.
    /// View entities (vw_SectionNavigation) are created as backing tables
    /// via raw SQL since SQLite doesn't support views in the same way.
    /// </remarks>
    /// <seealso cref="TableCellContextService"/>
    /// <seealso cref="TableCellContext"/>
    /// <seealso cref="TableCellContextFilter"/>
    [TestClass]
    public class TableCellContextServiceTests
    {
        #region Test Constants

        /// <summary>
        /// Known DocumentGUID for seeded test data.
        /// </summary>
        private static readonly Guid TestDocumentGuid = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");

        /// <summary>
        /// Known SectionGUID for seeded test data.
        /// </summary>
        private static readonly Guid TestSectionGuid = Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB");

        /// <summary>
        /// Second DocumentGUID for multi-document filtering tests.
        /// </summary>
        private static readonly Guid TestDocumentGuid2 = Guid.Parse("CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC");

        /// <summary>
        /// Second SectionGUID for multi-document filtering tests.
        /// </summary>
        private static readonly Guid TestSectionGuid2 = Guid.Parse("DDDDDDDD-DDDD-DDDD-DDDD-DDDDDDDDDDDD");

        #endregion

        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates an SQLite in-memory database context with the schema applied.
        /// View entities (vw_SectionNavigation) are created as backing tables via raw SQL
        /// since EF Core's ToView() registration excludes them from GenerateCreateScript().
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

            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();

            // Generate and patch DDL for SQLite compatibility
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
                catch (SqliteException)
                {
                    // Skip statements with unsupported SQL Server constructs
                }
            }

            // Create backing table for vw_SectionNavigation (registered as ToView, so excluded from DDL)
            using var createViewTable = connection.CreateCommand();
            createViewTable.CommandText = @"
                CREATE TABLE IF NOT EXISTS ""vw_SectionNavigation"" (
                    ""SectionID"" INTEGER,
                    ""SectionGUID"" TEXT,
                    ""SectionCode"" TEXT,
                    ""SectionType"" TEXT,
                    ""SectionTitle"" TEXT,
                    ""DocumentID"" INTEGER,
                    ""DocumentGUID"" TEXT,
                    ""SetGUID"" TEXT,
                    ""DocumentTitle"" TEXT,
                    ""VersionNumber"" INTEGER,
                    ""ParentSectionID"" INTEGER,
                    ""ParentSectionCode"" TEXT,
                    ""ParentSectionTitle"" TEXT,
                    ""ContentBlockCount"" INTEGER,
                    ""LabelerName"" TEXT
                )";
            createViewTable.ExecuteNonQuery();

            return context;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a <see cref="TableCellContextService"/> instance with the given context.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <returns>The configured service.</returns>
        private TableCellContextService createService(ApplicationDbContext context)
        {
            #region implementation
            var logger = new Mock<ILogger<TableCellContextService>>();
            return new TableCellContextService(context, logger.Object);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a minimal complete join chain: Document → SectionTextContent → TextTable →
        /// TextTableRow → TextTableCell(s), plus a matching vw_SectionNavigation row.
        /// Returns the seeded TextTableID for assertions.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="connection">The SQLite connection for raw SQL inserts.</param>
        /// <param name="documentGuid">DocumentGUID for the seeded document.</param>
        /// <param name="sectionGuid">SectionGUID for the seeded section navigation row.</param>
        /// <param name="textTableId">Starting TextTableID (for multi-table tests).</param>
        /// <param name="cellCount">Number of cells to seed in the single row.</param>
        /// <returns>The seeded TextTableID.</returns>
        private async Task<int> seedTestData(
            ApplicationDbContext context,
            SqliteConnection connection,
            Guid documentGuid,
            Guid sectionGuid,
            int textTableId = 100,
            int cellCount = 2)
        {
            #region implementation
            // Seed Document
            var document = new LabelContainer.Document
            {
                DocumentGUID = documentGuid,
                Title = "Test Drug Label",
                VersionNumber = 3,
            };
            context.Set<LabelContainer.Document>().Add(document);
            await context.SaveChangesAsync();
            var documentId = document.DocumentID!.Value;

            // Seed Section (needed for SectionTextContent FK)
            var section = new LabelContainer.Section
            {
                DocumentID = documentId,
                SectionGUID = sectionGuid,
                SectionCode = "34084-4",
            };
            context.Set<LabelContainer.Section>().Add(section);
            await context.SaveChangesAsync();
            var sectionId = section.SectionID!.Value;

            // Seed SectionTextContent
            var stc = new LabelContainer.SectionTextContent
            {
                SectionID = sectionId,
                ContentType = "Table",
                SequenceNumber = 1,
                ContentText = "Table content text",
            };
            context.Set<LabelContainer.SectionTextContent>().Add(stc);
            await context.SaveChangesAsync();
            var stcId = stc.SectionTextContentID!.Value;

            // Seed TextTable
            var textTable = new LabelContainer.TextTable
            {
                SectionTextContentID = stcId,
                Caption = "Dosage Table",
            };
            context.Set<LabelContainer.TextTable>().Add(textTable);
            await context.SaveChangesAsync();
            var ttId = textTable.TextTableID!.Value;

            // Seed TextTableRow
            var row = new LabelContainer.TextTableRow
            {
                TextTableID = ttId,
                RowGroupType = "Header",
                SequenceNumber = 1,
            };
            context.Set<LabelContainer.TextTableRow>().Add(row);
            await context.SaveChangesAsync();
            var rowId = row.TextTableRowID!.Value;

            // Seed TextTableCells
            for (int i = 1; i <= cellCount; i++)
            {
                var cell = new LabelContainer.TextTableCell
                {
                    TextTableRowID = rowId,
                    CellType = i == 1 ? "th" : "td",
                    CellText = $"Cell {i} Text",
                    SequenceNumber = i,
                    RowSpan = 1,
                    ColSpan = i == 1 ? 2 : 1,
                };
                context.Set<LabelContainer.TextTableCell>().Add(cell);
            }
            await context.SaveChangesAsync();

            // Seed vw_SectionNavigation backing table via raw SQL
            // EF Core 8 SQLite sends GUIDs as uppercase TEXT with hyphens
            using var insertNav = connection.CreateCommand();
            insertNav.CommandText = @"
                INSERT INTO ""vw_SectionNavigation""
                    (""SectionID"", ""SectionGUID"", ""SectionCode"", ""SectionType"", ""SectionTitle"",
                     ""DocumentID"", ""DocumentGUID"", ""SetGUID"", ""DocumentTitle"", ""VersionNumber"",
                     ""ParentSectionID"", ""ParentSectionCode"", ""ParentSectionTitle"", ""ContentBlockCount"",
                     ""LabelerName"")
                VALUES
                    ($sectionId, $sectionGuid, '34084-4', 'ADVERSE REACTIONS', 'Adverse Reactions',
                     $documentId, $documentGuid, $documentGuid, 'Test Drug Label', 3,
                     NULL, NULL, NULL, 1,
                     'Test Pharma Inc')";
            insertNav.Parameters.AddWithValue("$sectionId", sectionId);
            insertNav.Parameters.AddWithValue("$sectionGuid", sectionGuid.ToString("D").ToUpper());
            insertNav.Parameters.AddWithValue("$documentId", documentId);
            insertNav.Parameters.AddWithValue("$documentGuid", documentGuid.ToString("D").ToUpper());
            insertNav.ExecuteNonQuery();

            return ttId;
            #endregion
        }

        #endregion

        #region Constructor Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that a null context parameter throws <see cref="ArgumentNullException"/>.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullContext_ThrowsArgumentNullException()
        {
            #region implementation
            var logger = new Mock<ILogger<TableCellContextService>>();
            _ = new TableCellContextService(null!, logger.Object);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a null logger parameter throws <see cref="ArgumentNullException"/>.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            #region implementation
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            var context = createTestContext(connection);
            _ = new TableCellContextService(context, null!);
            #endregion
        }

        #endregion

        #region GetTableCellContextsAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that with no filter, all seeded rows are returned via the full join chain.
        /// </summary>
        [TestMethod]
        public async Task GetTableCellContextsAsync_NoFilter_ReturnsAllSeededRows()
        {
            #region implementation
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            using var context = createTestContext(connection);
            await seedTestData(context, connection, TestDocumentGuid, TestSectionGuid, cellCount: 2);

            var service = createService(context);
            var results = await service.GetTableCellContextsAsync();

            Assert.AreEqual(2, results.Count, "Expected 2 cells from seeded data");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that filtering by DocumentGUID returns only the matching document's cells.
        /// </summary>
        [TestMethod]
        public async Task GetTableCellContextsAsync_FilterByDocumentGuid_FiltersCorrectly()
        {
            #region implementation
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            using var context = createTestContext(connection);

            // Seed two documents
            await seedTestData(context, connection, TestDocumentGuid, TestSectionGuid, cellCount: 2);
            await seedTestData(context, connection, TestDocumentGuid2, TestSectionGuid2, cellCount: 1);

            var service = createService(context);
            var filter = new TableCellContextFilter { DocumentGUID = TestDocumentGuid };
            var results = await service.GetTableCellContextsAsync(filter);

            Assert.AreEqual(2, results.Count, "Only doc 1's cells should be returned");
            Assert.IsTrue(results.All(r => r.DocumentGUID == TestDocumentGuid));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that filtering by TextTableID returns only the matching table's cells.
        /// </summary>
        [TestMethod]
        public async Task GetTableCellContextsAsync_FilterByTextTableId_FiltersCorrectly()
        {
            #region implementation
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            using var context = createTestContext(connection);

            var ttId1 = await seedTestData(context, connection, TestDocumentGuid, TestSectionGuid, cellCount: 2);
            var ttId2 = await seedTestData(context, connection, TestDocumentGuid2, TestSectionGuid2, cellCount: 1);

            var service = createService(context);
            var filter = new TableCellContextFilter { TextTableID = ttId1 };
            var results = await service.GetTableCellContextsAsync(filter);

            Assert.AreEqual(2, results.Count, "Only table 1's cells should be returned");
            Assert.IsTrue(results.All(r => r.TextTableID == ttId1));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that range filtering is inclusive on both bounds.
        /// </summary>
        [TestMethod]
        public async Task GetTableCellContextsAsync_FilterByRange_FiltersCorrectly()
        {
            #region implementation
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            using var context = createTestContext(connection);

            var ttId1 = await seedTestData(context, connection, TestDocumentGuid, TestSectionGuid, cellCount: 1);
            var ttId2 = await seedTestData(context, connection, TestDocumentGuid2, TestSectionGuid2, cellCount: 1);

            var service = createService(context);

            // Range that includes only the first table
            var filter = new TableCellContextFilter
            {
                TextTableIdRangeStart = ttId1,
                TextTableIdRangeEnd = ttId1,
            };
            var results = await service.GetTableCellContextsAsync(filter);

            Assert.AreEqual(1, results.Count, "Range should include only table 1");
            Assert.AreEqual(ttId1, results[0].TextTableID);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that MaxRows applies .Take(N) to limit results.
        /// </summary>
        [TestMethod]
        public async Task GetTableCellContextsAsync_WithMaxRows_LimitsResults()
        {
            #region implementation
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            using var context = createTestContext(connection);
            await seedTestData(context, connection, TestDocumentGuid, TestSectionGuid, cellCount: 5);

            var service = createService(context);
            var filter = new TableCellContextFilter { MaxRows = 3 };
            var results = await service.GetTableCellContextsAsync(filter);

            Assert.AreEqual(3, results.Count, "MaxRows=3 should return exactly 3 rows");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that all 26 DTO properties are populated from seeded data.
        /// </summary>
        [TestMethod]
        public async Task GetTableCellContextsAsync_ProjectsMapsAllFields()
        {
            #region implementation
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            using var context = createTestContext(connection);
            await seedTestData(context, connection, TestDocumentGuid, TestSectionGuid, cellCount: 1);

            var service = createService(context);
            var results = await service.GetTableCellContextsAsync();

            Assert.AreEqual(1, results.Count);
            var row = results[0];

            // Cell properties
            Assert.IsNotNull(row.TextTableCellID, "TextTableCellID");
            Assert.AreEqual("th", row.CellType, "CellType");
            Assert.AreEqual("Cell 1 Text", row.CellText, "CellText");
            Assert.AreEqual(1, row.SequenceNumber, "SequenceNumber");
            Assert.AreEqual(1, row.RowSpan, "RowSpan");
            Assert.AreEqual(2, row.ColSpan, "ColSpan");

            // Row properties
            Assert.IsNotNull(row.TextTableRowID, "TextTableRowID");
            Assert.AreEqual("Header", row.RowGroupType, "RowGroupType");
            Assert.AreEqual(1, row.SequenceNumberTextTableRow, "SequenceNumberTextTableRow");

            // Table properties
            Assert.IsNotNull(row.TextTableID, "TextTableID");
            Assert.IsNotNull(row.SectionTextContentID, "SectionTextContentID");
            Assert.AreEqual("Dosage Table", row.Caption, "Caption");

            // Content properties
            Assert.AreEqual("Table", row.ContentType, "ContentType");
            Assert.AreEqual(1, row.SequenceNumberSectionTextContent, "SequenceNumberSectionTextContent");
            Assert.AreEqual("Table content text", row.ContentText, "ContentText");

            // Document properties
            Assert.AreEqual(TestDocumentGuid, row.DocumentGUID, "DocumentGUID");
            Assert.AreEqual("Test Drug Label", row.Title, "Title");
            Assert.AreEqual(3, row.VersionNumber, "VersionNumber");

            // Section navigation properties
            Assert.AreEqual(TestSectionGuid, row.SectionGUID, "SectionGUID");
            Assert.AreEqual("34084-4", row.SectionCode, "SectionCode");
            Assert.AreEqual("ADVERSE REACTIONS", row.SectionType, "SectionType");
            Assert.AreEqual("Adverse Reactions", row.SectionTitle, "SectionTitle");
            Assert.IsNull(row.ParentSectionID, "ParentSectionID should be null for root section");
            Assert.IsNull(row.ParentSectionCode, "ParentSectionCode should be null for root section");
            Assert.IsNull(row.ParentSectionTitle, "ParentSectionTitle should be null for root section");
            Assert.AreEqual("Test Pharma Inc", row.LabelerName, "LabelerName");
            #endregion
        }

        #endregion

        #region GetTableCellContextsGroupedByTableAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that results are correctly grouped by TextTableID with distinct keys.
        /// </summary>
        [TestMethod]
        public async Task GetTableCellContextsGroupedByTableAsync_GroupsByTextTableId()
        {
            #region implementation
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            using var context = createTestContext(connection);

            var ttId1 = await seedTestData(context, connection, TestDocumentGuid, TestSectionGuid, cellCount: 2);
            var ttId2 = await seedTestData(context, connection, TestDocumentGuid2, TestSectionGuid2, cellCount: 1);

            var service = createService(context);
            var grouped = await service.GetTableCellContextsGroupedByTableAsync();

            Assert.AreEqual(2, grouped.Count, "Should have 2 table groups");
            Assert.IsTrue(grouped.ContainsKey(ttId1), "Should contain table 1");
            Assert.IsTrue(grouped.ContainsKey(ttId2), "Should contain table 2");
            Assert.AreEqual(2, grouped[ttId1].Count, "Table 1 should have 2 cells");
            Assert.AreEqual(1, grouped[ttId2].Count, "Table 2 should have 1 cell");
            #endregion
        }

        #endregion

        #region GetTextTableIdRangeAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that min and max TextTableID values are returned from seeded data.
        /// </summary>
        [TestMethod]
        public async Task GetTextTableIdRangeAsync_ReturnsMinAndMax()
        {
            #region implementation
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            using var context = createTestContext(connection);

            var ttId1 = await seedTestData(context, connection, TestDocumentGuid, TestSectionGuid, cellCount: 1);
            var ttId2 = await seedTestData(context, connection, TestDocumentGuid2, TestSectionGuid2, cellCount: 1);

            var service = createService(context);
            var (minId, maxId) = await service.GetTextTableIdRangeAsync();

            Assert.AreEqual(ttId1, minId, "MinId should be the first seeded table ID");
            Assert.AreEqual(ttId2, maxId, "MaxId should be the second seeded table ID");
            Assert.IsTrue(minId <= maxId, "MinId should be <= MaxId");
            #endregion
        }

        #endregion

        #region TableCellContextFilter Validation Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that combining TextTableID with range parameters throws.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TableCellContextFilter_Validate_TextTableIdAndRange_Throws()
        {
            #region implementation
            var filter = new TableCellContextFilter
            {
                TextTableID = 5,
                TextTableIdRangeStart = 1,
                TextTableIdRangeEnd = 10,
            };
            filter.Validate();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that specifying only one range bound throws.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TableCellContextFilter_Validate_PartialRange_Throws()
        {
            #region implementation
            var filter = new TableCellContextFilter
            {
                TextTableIdRangeStart = 1,
            };
            filter.Validate();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that MaxRows = 0 throws.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TableCellContextFilter_Validate_MaxRowsZero_Throws()
        {
            #region implementation
            var filter = new TableCellContextFilter { MaxRows = 0 };
            filter.Validate();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a valid filter passes validation without exception.
        /// </summary>
        [TestMethod]
        public void TableCellContextFilter_Validate_ValidFilter_DoesNotThrow()
        {
            #region implementation
            var filter = new TableCellContextFilter
            {
                TextTableIdRangeStart = 1,
                TextTableIdRangeEnd = 1000,
                MaxRows = 500,
                DocumentGUID = TestDocumentGuid,
            };
            filter.Validate(); // Should not throw
            #endregion
        }

        #endregion
    }
}
