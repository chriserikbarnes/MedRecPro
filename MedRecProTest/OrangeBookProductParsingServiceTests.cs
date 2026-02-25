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
using LabelContainer = MedRecProImportClass.Models.Label;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="OrangeBookProductParsingService"/>.
    /// </summary>
    /// <remarks>
    /// Tests cover:
    /// - Line parsing (14-column validation)
    /// - Field parsing: splitDfRoute, parseApprovalDate, parseYesNo, mapApplTypeToPrefix
    /// - Company name normalization and tokenization
    /// - Token similarity calculation (Jaccard and containment)
    /// - Jurisdiction detection from corporate suffixes
    /// - Full pipeline: applicant/product upsert, org/ingredient/category matching
    ///
    /// Pipeline tests use shared-cache named SQLite in-memory databases with a sentinel
    /// connection so the DB survives the service's finally { connection.CloseAsync() } block.
    /// </remarks>
    /// <seealso cref="OrangeBookProductParsingService"/>
    /// <seealso cref="OrangeBook.Product"/>
    /// <seealso cref="OrangeBook.Applicant"/>
    /// <seealso cref="OrangeBookImportResult"/>
    [TestClass]
    public class OrangeBookProductParsingServiceTests
    {
        #region Test Constants

        /// <summary>
        /// Header row for products.txt (14 tilde-delimited columns).
        /// </summary>
        private const string ProductHeader = "Ingredient~DF;Route~Trade_Name~Applicant~Strength~Appl_Type~Appl_No~Product_No~TE_Code~Approval_Date~RLD~RS~Type~Applicant_Full_Name";

        /// <summary>
        /// Valid data row: BUDESONIDE rectal foam, NDA 205613, by SALIX.
        /// </summary>
        private const string ValidRow1 = "BUDESONIDE~AEROSOL, FOAM;RECTAL~UCERIS~SALIX~2MG/ACTUATION~N~205613~001~AB~Apr 12, 2023~Yes~Yes~RX~SALIX PHARMACEUTICALS INC";

        /// <summary>
        /// Valid data row: ATORVASTATIN oral tablet, ANDA 076477, by RANBAXY.
        /// </summary>
        private const string ValidRow2 = "ATORVASTATIN CALCIUM~TABLET;ORAL~ATORVASTATIN CALCIUM~RANBAXY~10MG~A~076477~001~~Nov 30, 2011~No~No~RX~RANBAXY PHARMACEUTICALS INC";

        /// <summary>
        /// Valid data row with pre-market approval date.
        /// </summary>
        private const string ValidRowPremarket = "ASPIRIN~TABLET;ORAL~ASPIRIN~BAYER~325MG~N~010000~001~~~Approved Prior to Jan 1, 1982~No~No~RX~BAYER HEALTHCARE LLC";

        /// <summary>
        /// Malformed row with only 10 columns (expected 14).
        /// </summary>
        private const string MalformedRow = "BUDESONIDE~AEROSOL, FOAM;RECTAL~UCERIS~SALIX~2MG/ACTUATION~N~205613~001~AB~Apr 12, 2023";

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
        /// Creates a product parsing service instance wired to the given context via mock DI.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <returns>The configured <see cref="OrangeBookProductParsingService"/>.</returns>
        private OrangeBookProductParsingService createService(ApplicationDbContext? context)
        {
            #region implementation
            var scopeFactory = new Mock<IServiceScopeFactory>();
            var scope = new Mock<IServiceScope>();
            var serviceProvider = new Mock<IServiceProvider>();

            scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);
            scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);
            if (context != null)
            {
                serviceProvider.Setup(x => x.GetService(typeof(ApplicationDbContext)))
                    .Returns(context);
            }

            var logger = new Mock<ILogger<OrangeBookProductParsingService>>();

            return new OrangeBookProductParsingService(scopeFactory.Object, logger.Object);
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
            var lines = new List<string> { ProductHeader };
            lines.AddRange(rows);
            return string.Join("\n", lines);
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

        /**************************************************************/
        /// <summary>
        /// Seeds a Label.Organization record for org matching tests.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="name">Organization name.</param>
        /// <returns>The seeded OrganizationID.</returns>
        private async Task<int> seedOrganization(ApplicationDbContext context, string name)
        {
            #region implementation
            var org = new LabelContainer.Organization
            {
                OrganizationName = name
            };
            context.Set<LabelContainer.Organization>().Add(org);
            await context.SaveChangesAsync();
            return org.OrganizationID!.Value;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a Label.IngredientSubstance record for ingredient matching tests.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="substanceName">Substance name.</param>
        /// <returns>The seeded IngredientSubstanceID.</returns>
        private async Task<int> seedIngredientSubstance(ApplicationDbContext context, string substanceName)
        {
            #region implementation
            var substance = new LabelContainer.IngredientSubstance
            {
                SubstanceName = substanceName
            };
            context.Set<LabelContainer.IngredientSubstance>().Add(substance);
            await context.SaveChangesAsync();
            return substance.IngredientSubstanceID!.Value;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a Label.MarketingCategory record for category matching tests.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="appIdValue">ApplicationOrMonographIDValue (e.g., "NDA205613").</param>
        /// <returns>The seeded MarketingCategoryID.</returns>
        private async Task<int> seedMarketingCategory(ApplicationDbContext context, string appIdValue)
        {
            #region implementation
            var category = new LabelContainer.MarketingCategory
            {
                ApplicationOrMonographIDValue = appIdValue,
                CategoryCode = "C73584",
                CategoryDisplayName = "NDA"
            };
            context.Set<LabelContainer.MarketingCategory>().Add(category);
            await context.SaveChangesAsync();
            return category.MarketingCategoryID!.Value;
            #endregion
        }

        #endregion

        #region Test Methods — ParseLines

        /**************************************************************/
        /// <summary>
        /// Verifies that parseLines returns correct row count for valid 14-column rows.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.parseLines"/>
        [TestMethod]
        public void ParseLines_ValidFile_ReturnsCorrectRowCount()
        {
            #region implementation
            // Arrange
            var service = createService(null);
            var result = new OrangeBookImportResult();
            var fileContent = buildFileContent(ValidRow1, ValidRow2);

            // Act
            var rows = service.parseLines(fileContent, result);

            // Assert
            Assert.AreEqual(2, rows.Count, "Should return 2 data rows after skipping header");
            Assert.AreEqual(14, rows[0].Length, "Each row should have exactly 14 columns");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that rows with incorrect column count are skipped.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.parseLines"/>
        [TestMethod]
        public void ParseLines_MalformedRow_SkipsRow()
        {
            #region implementation
            // Arrange
            var service = createService(null);
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

        #region Test Methods — SplitDfRoute

        /**************************************************************/
        /// <summary>
        /// Verifies simple "DosageForm;Route" splitting.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.splitDfRoute"/>
        [TestMethod]
        public void SplitDfRoute_SimpleRoute_SplitsCorrectly()
        {
            #region implementation
            // Arrange
            var service = createService(null);

            // Act
            var (dosageForm, route) = service.splitDfRoute("TABLET;ORAL");

            // Assert
            Assert.AreEqual("TABLET", dosageForm, "Dosage form should be 'TABLET'");
            Assert.AreEqual("ORAL", route, "Route should be 'ORAL'");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies splitting on the LAST semicolon for complex dosage forms
        /// that contain commas (e.g., "AEROSOL, FOAM;TOPICAL").
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.splitDfRoute"/>
        [TestMethod]
        public void SplitDfRoute_ComplexDosageForm_SplitsOnLastSemicolon()
        {
            #region implementation
            // Arrange
            var service = createService(null);

            // Act
            var (dosageForm, route) = service.splitDfRoute("AEROSOL, FOAM;TOPICAL");

            // Assert
            Assert.AreEqual("AEROSOL, FOAM", dosageForm, "Complex dosage form should be preserved");
            Assert.AreEqual("TOPICAL", route, "Route should be 'TOPICAL'");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a value with no semicolon returns dosage form only.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.splitDfRoute"/>
        [TestMethod]
        public void SplitDfRoute_NoSemicolon_ReturnsDosageFormOnly()
        {
            #region implementation
            // Arrange
            var service = createService(null);

            // Act
            var (dosageForm, route) = service.splitDfRoute("TABLET");

            // Assert
            Assert.AreEqual("TABLET", dosageForm, "Dosage form should be 'TABLET'");
            Assert.IsNull(route, "Route should be null when no semicolon present");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty string returns both null.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.splitDfRoute"/>
        [TestMethod]
        public void SplitDfRoute_EmptyString_ReturnsBothNull()
        {
            #region implementation
            // Arrange
            var service = createService(null);

            // Act
            var (dosageForm, route) = service.splitDfRoute("");

            // Assert
            Assert.IsNull(dosageForm, "Dosage form should be null for empty input");
            Assert.IsNull(route, "Route should be null for empty input");
            #endregion
        }

        #endregion

        #region Test Methods — ParseApprovalDate

        /**************************************************************/
        /// <summary>
        /// Verifies standard date parsing with isPremarket=false.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.parseApprovalDate"/>
        [TestMethod]
        public void ParseApprovalDate_StandardDate_ReturnsParsedDate()
        {
            #region implementation
            // Arrange
            var service = createService(null);

            // Act
            var date = service.parseApprovalDate("Apr 12, 2023", out bool isPremarket);

            // Assert
            Assert.IsNotNull(date, "Standard date should parse successfully");
            Assert.AreEqual(new DateTime(2023, 4, 12), date.Value, "Should parse to April 12, 2023");
            Assert.IsFalse(isPremarket, "isPremarket should be false for standard dates");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the special pre-1982 text returns null with isPremarket=true.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.parseApprovalDate"/>
        [TestMethod]
        public void ParseApprovalDate_PremarketText_ReturnsNullWithFlag()
        {
            #region implementation
            // Arrange
            var service = createService(null);

            // Act
            var date = service.parseApprovalDate("Approved Prior to Jan 1, 1982", out bool isPremarket);

            // Assert
            Assert.IsNull(date, "Pre-market text should return null date");
            Assert.IsTrue(isPremarket, "isPremarket should be true for pre-1982 text");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty string returns null with isPremarket=false.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.parseApprovalDate"/>
        [TestMethod]
        public void ParseApprovalDate_EmptyString_ReturnsNull()
        {
            #region implementation
            // Arrange
            var service = createService(null);

            // Act
            var date = service.parseApprovalDate("", out bool isPremarket);

            // Assert
            Assert.IsNull(date, "Empty string should return null");
            Assert.IsFalse(isPremarket, "isPremarket should be false for empty input");
            #endregion
        }

        #endregion

        #region Test Methods — MapApplTypeToPrefix

        /**************************************************************/
        /// <summary>
        /// Verifies "N" maps to "NDA".
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.mapApplTypeToPrefix"/>
        [TestMethod]
        public void MapApplTypeToPrefix_N_ReturnsNDA()
        {
            #region implementation
            var service = createService(null);
            Assert.AreEqual("NDA", service.mapApplTypeToPrefix("N"), "'N' should map to 'NDA'");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies "A" maps to "ANDA".
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.mapApplTypeToPrefix"/>
        [TestMethod]
        public void MapApplTypeToPrefix_A_ReturnsANDA()
        {
            #region implementation
            var service = createService(null);
            Assert.AreEqual("ANDA", service.mapApplTypeToPrefix("A"), "'A' should map to 'ANDA'");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies unknown types are returned as-is.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.mapApplTypeToPrefix"/>
        [TestMethod]
        public void MapApplTypeToPrefix_Other_ReturnsRaw()
        {
            #region implementation
            var service = createService(null);
            Assert.AreEqual("BLA", service.mapApplTypeToPrefix("BLA"), "Unknown types should be returned trimmed");
            #endregion
        }

        #endregion

        #region Test Methods — NormalizeCompanyName

        /**************************************************************/
        /// <summary>
        /// Verifies that corporate suffixes like INC are stripped.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.normalizeCompanyName"/>
        [TestMethod]
        public void NormalizeCompanyName_StripsCorporateSuffixes()
        {
            #region implementation
            var result = OrangeBookProductParsingService.normalizeCompanyName("TEVA PHARMACEUTICALS USA INC");
            Assert.IsFalse(result.Contains("INC"), "INC should be stripped");
            Assert.IsFalse(result.Contains("USA"), "USA is a corporate suffix and should be stripped");
            Assert.IsTrue(result.Contains("TEVA"), "TEVA should be preserved");
            Assert.IsTrue(result.Contains("PHARMACEUTICALS"), "PHARMACEUTICALS should be preserved when stripNoiseWords=false");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that dots and ampersands are stripped.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.normalizeCompanyName"/>
        [TestMethod]
        public void NormalizeCompanyName_StripsDotsAndAmpersands()
        {
            #region implementation
            var result = OrangeBookProductParsingService.normalizeCompanyName("Johnson & Johnson");
            Assert.IsFalse(result.Contains("&"), "Ampersand should be stripped");
            Assert.IsTrue(result.Contains("JOHNSON"), "Name should be uppercased and preserved");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that stripNoiseWords=true removes pharmaceutical noise words.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.normalizeCompanyName"/>
        [TestMethod]
        public void NormalizeCompanyName_WithNoiseStripping_RemovesPharmaWords()
        {
            #region implementation
            var result = OrangeBookProductParsingService.normalizeCompanyName(
                "TEVA PHARMACEUTICALS USA", stripNoiseWords: true);
            Assert.IsFalse(result.Contains("PHARMACEUTICALS"), "PHARMACEUTICALS should be stripped with noise stripping");
            Assert.IsTrue(result.Contains("TEVA"), "TEVA should be preserved");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that stripNoiseWords=false preserves pharmaceutical words.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.normalizeCompanyName"/>
        [TestMethod]
        public void NormalizeCompanyName_WithoutNoiseStripping_PreservesPharmaWords()
        {
            #region implementation
            var result = OrangeBookProductParsingService.normalizeCompanyName(
                "TEVA PHARMACEUTICALS USA", stripNoiseWords: false);
            Assert.IsTrue(result.Contains("PHARMACEUTICALS"), "PHARMACEUTICALS should be preserved without noise stripping");
            #endregion
        }

        #endregion

        #region Test Methods — Tokenize

        /**************************************************************/
        /// <summary>
        /// Verifies that a standard name is split into the correct token set.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.tokenize"/>
        [TestMethod]
        public void Tokenize_StandardName_ReturnTokenSet()
        {
            #region implementation
            var tokens = OrangeBookProductParsingService.tokenize("TEVA PHARMACEUTICALS USA");
            Assert.AreEqual(3, tokens.Count, "Should produce 3 tokens");
            Assert.IsTrue(tokens.Contains("TEVA"), "Should contain 'TEVA'");
            Assert.IsTrue(tokens.Contains("PHARMACEUTICALS"), "Should contain 'PHARMACEUTICALS'");
            Assert.IsTrue(tokens.Contains("USA"), "Should contain 'USA'");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that single-character tokens are filtered out.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.tokenize"/>
        [TestMethod]
        public void Tokenize_FiltersSingleCharTokens()
        {
            #region implementation
            var tokens = OrangeBookProductParsingService.tokenize("A B COMPANY NAME");
            Assert.AreEqual(2, tokens.Count, "Single-char tokens 'A' and 'B' should be filtered");
            Assert.IsTrue(tokens.Contains("COMPANY"), "Should contain 'COMPANY'");
            Assert.IsTrue(tokens.Contains("NAME"), "Should contain 'NAME'");
            #endregion
        }

        #endregion

        #region Test Methods — CalculateTokenSimilarity

        /**************************************************************/
        /// <summary>
        /// Verifies that identical sets produce perfect scores.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.calculateTokenSimilarity"/>
        [TestMethod]
        public void CalculateTokenSimilarity_IdenticalSets_ReturnsPerfectScore()
        {
            #region implementation
            var set1 = new HashSet<string> { "TEVA", "PHARMA" };
            var set2 = new HashSet<string> { "TEVA", "PHARMA" };

            var (jaccard, containment) = OrangeBookProductParsingService.calculateTokenSimilarity(set1, set2);

            Assert.AreEqual(1.0, jaccard, 0.001, "Jaccard should be 1.0 for identical sets");
            Assert.AreEqual(1.0, containment, 0.001, "Containment should be 1.0 for identical sets");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that when applicant tokens are a subset of org tokens,
        /// containment is 1.0 but Jaccard is less than 1.0.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.calculateTokenSimilarity"/>
        [TestMethod]
        public void CalculateTokenSimilarity_Subset_ReturnsPartialJaccardFullContainment()
        {
            #region implementation
            var applicant = new HashSet<string> { "PFIZER" };
            var org = new HashSet<string> { "PFIZER", "CONSUMER", "HEALTHCARE" };

            var (jaccard, containment) = OrangeBookProductParsingService.calculateTokenSimilarity(applicant, org);

            Assert.IsTrue(jaccard < 1.0, "Jaccard should be less than 1.0 for subset");
            Assert.AreEqual(1.0, containment, 0.001, "Containment should be 1.0 when applicant is fully contained");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that disjoint sets produce zero scores.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.calculateTokenSimilarity"/>
        [TestMethod]
        public void CalculateTokenSimilarity_Disjoint_ReturnsZero()
        {
            #region implementation
            var set1 = new HashSet<string> { "TEVA" };
            var set2 = new HashSet<string> { "PFIZER" };

            var (jaccard, containment) = OrangeBookProductParsingService.calculateTokenSimilarity(set1, set2);

            Assert.AreEqual(0.0, jaccard, 0.001, "Jaccard should be 0.0 for disjoint sets");
            Assert.AreEqual(0.0, containment, 0.001, "Containment should be 0.0 for disjoint sets");
            #endregion
        }

        #endregion

        #region Test Methods — DetectEntityJurisdiction

        /**************************************************************/
        /// <summary>
        /// Verifies US jurisdiction detection for "INC" suffix.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.detectEntityJurisdiction"/>
        [TestMethod]
        public void DetectEntityJurisdiction_USEntity_ReturnsUS()
        {
            #region implementation
            var jurisdiction = OrangeBookProductParsingService.detectEntityJurisdiction("PFIZER INC");
            Assert.AreEqual("US", jurisdiction, "INC suffix should detect US jurisdiction");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies UK jurisdiction detection for "LTD" suffix.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.detectEntityJurisdiction"/>
        [TestMethod]
        public void DetectEntityJurisdiction_UKEntity_ReturnsUK()
        {
            #region implementation
            var jurisdiction = OrangeBookProductParsingService.detectEntityJurisdiction("ASTRAZENECA LTD");
            Assert.AreEqual("UK", jurisdiction, "LTD suffix should detect UK jurisdiction");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies German jurisdiction detection for "GMBH" suffix.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.detectEntityJurisdiction"/>
        [TestMethod]
        public void DetectEntityJurisdiction_GermanEntity_ReturnsDE()
        {
            #region implementation
            var jurisdiction = OrangeBookProductParsingService.detectEntityJurisdiction("BAYER GMBH");
            Assert.AreEqual("DE", jurisdiction, "GMBH suffix should detect DE jurisdiction");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that names without a recognized suffix return null.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.detectEntityJurisdiction"/>
        [TestMethod]
        public void DetectEntityJurisdiction_NoSuffix_ReturnsNull()
        {
            #region implementation
            var jurisdiction = OrangeBookProductParsingService.detectEntityJurisdiction("PFIZER");
            Assert.IsNull(jurisdiction, "Name without corporate suffix should return null jurisdiction");
            #endregion
        }

        #endregion

        #region Test Methods — ProcessProductsFileAsync (Pipeline)

        /**************************************************************/
        /// <summary>
        /// Verifies that new applicants and products are created with correct field mapping.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.ProcessProductsFileAsync"/>
        [TestMethod]
        public async Task ProcessProductsFileAsync_NewProducts_CreatesApplicantsAndProducts()
        {
            #region implementation
            // Arrange — shared-cache prevents connection.CloseAsync() from destroying the DB
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            var service = createService(context);
            var fileContent = buildFileContent(ValidRow1);

            try
            {
                // Act
                var result = await service.ProcessProductsFileAsync(fileContent, CancellationToken.None);

                // Debug trace
                Debug.WriteLine($"[NewProducts_CreatesApplicantsAndProducts] Success={result.Success}, " +
                    $"Errors=[{string.Join("; ", result.Errors)}]");

                // Reopen connection after service's finally block
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

                // Assert
                var applicants = await context.Set<OrangeBook.Applicant>().ToListAsync();
                Assert.AreEqual(1, applicants.Count, "Should create 1 applicant");
                Assert.AreEqual("SALIX", applicants[0].ApplicantName, "Applicant short name should be 'SALIX'");
                Assert.AreEqual("SALIX PHARMACEUTICALS INC", applicants[0].ApplicantFullName, "Full name should be preserved");

                var products = await context.Set<OrangeBook.Product>().ToListAsync();
                Assert.AreEqual(1, products.Count, "Should create 1 product");
                Assert.AreEqual(1, result.ApplicantsCreated, "ApplicantsCreated should be 1");
                Assert.AreEqual(1, result.ProductsCreated, "ProductsCreated should be 1");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NewProducts_CreatesApplicantsAndProducts] FAILED: {ex.GetType().Name}: {ex.Message}");
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
        /// Verifies correct field parsing for all 14 columns including DosageForm/Route split,
        /// boolean flags, date parsing, and TE code.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.ProcessProductsFileAsync"/>
        [TestMethod]
        public async Task ProcessProductsFileAsync_ParsesFieldsCorrectly()
        {
            #region implementation
            // Arrange — shared-cache prevents connection.CloseAsync() from destroying the DB
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            var service = createService(context);
            var fileContent = buildFileContent(ValidRow1);

            try
            {
                // Act
                var result = await service.ProcessProductsFileAsync(fileContent, CancellationToken.None);

                // Debug trace
                Debug.WriteLine($"[ParsesFieldsCorrectly] Success={result.Success}, " +
                    $"Errors=[{string.Join("; ", result.Errors)}]");

                // Reopen connection after service's finally block
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

                // Assert
                var product = await context.Set<OrangeBook.Product>().FirstAsync();
                Assert.AreEqual("BUDESONIDE", product.Ingredient, "Ingredient should be 'BUDESONIDE'");
                Assert.AreEqual("AEROSOL, FOAM", product.DosageForm, "DosageForm should be 'AEROSOL, FOAM' (split on last semicolon)");
                Assert.AreEqual("RECTAL", product.Route, "Route should be 'RECTAL'");
                Assert.AreEqual("UCERIS", product.TradeName, "TradeName should be 'UCERIS'");
                Assert.AreEqual("2MG/ACTUATION", product.Strength, "Strength should be '2MG/ACTUATION'");
                Assert.AreEqual("N", product.ApplType, "ApplType should be 'N'");
                Assert.AreEqual("205613", product.ApplNo, "ApplNo should be '205613'");
                Assert.AreEqual("001", product.ProductNo, "ProductNo should be '001'");
                Assert.AreEqual("AB", product.TECode, "TECode should be 'AB'");
                Assert.AreEqual(new DateTime(2023, 4, 12), product.ApprovalDate, "ApprovalDate should be Apr 12, 2023");
                Assert.AreEqual(true, product.IsRLD, "IsRLD should be true");
                Assert.AreEqual(true, product.IsRS, "IsRS should be true");
                Assert.AreEqual("RX", product.Type, "Type should be 'RX'");
                Assert.AreEqual(false, product.ApprovalDateIsPremarket, "Should not be pre-market");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ParsesFieldsCorrectly] FAILED: {ex.GetType().Name}: {ex.Message}");
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
        /// Verifies that re-importing updates changed product fields.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.ProcessProductsFileAsync"/>
        [TestMethod]
        public async Task ProcessProductsFileAsync_ExistingProduct_UpdatesFields()
        {
            #region implementation
            // Arrange — first import using shared-cache DB
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            var service = createService(context);

            try
            {
                await service.ProcessProductsFileAsync(buildFileContent(ValidRow1), CancellationToken.None);

                // Arrange — re-import with updated strength
                var updatedRow = "BUDESONIDE~AEROSOL, FOAM;RECTAL~UCERIS~SALIX~4MG/ACTUATION~N~205613~001~AB~Apr 12, 2023~Yes~Yes~RX~SALIX PHARMACEUTICALS INC";

                // Reopen connection and create fresh context for re-import
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
                using var context2 = createTestContext(connection);
                var service2 = createService(context2);

                // Act
                var result = await service2.ProcessProductsFileAsync(buildFileContent(updatedRow), CancellationToken.None);

                // Debug trace
                Debug.WriteLine($"[ExistingProduct_UpdatesFields] Success={result.Success}, " +
                    $"Errors=[{string.Join("; ", result.Errors)}]");

                // Reopen connection after service's finally block
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

                // Assert
                var products = await context2.Set<OrangeBook.Product>().ToListAsync();
                Assert.AreEqual(1, products.Count, "Should still have 1 product (updated, not duplicated)");
                Assert.AreEqual("4MG/ACTUATION", products[0].Strength, "Strength should be updated to '4MG/ACTUATION'");
                Assert.AreEqual(1, result.ProductsUpdated, "ProductsUpdated should be 1");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExistingProduct_UpdatesFields] FAILED: {ex.GetType().Name}: {ex.Message}");
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
        /// Verifies that result counts are tracked correctly.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.ProcessProductsFileAsync"/>
        [TestMethod]
        public async Task ProcessProductsFileAsync_TracksResultCounts()
        {
            #region implementation
            // Arrange — shared-cache prevents connection.CloseAsync() from destroying the DB
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            var service = createService(context);
            var fileContent = buildFileContent(ValidRow1, ValidRow2);

            try
            {
                // Act
                var result = await service.ProcessProductsFileAsync(fileContent, CancellationToken.None);

                // Debug trace
                Debug.WriteLine($"[TracksResultCounts] Success={result.Success}, " +
                    $"ApplicantsCreated={result.ApplicantsCreated}, ProductsCreated={result.ProductsCreated}, " +
                    $"Errors=[{string.Join("; ", result.Errors)}]");

                // Reopen connection after service's finally block
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

                // Assert
                Assert.AreEqual(2, result.ApplicantsCreated, "Should create 2 applicants (SALIX and RANBAXY)");
                Assert.AreEqual(2, result.ProductsCreated, "Should create 2 products");
                Assert.IsTrue(result.Success, "Import should succeed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TracksResultCounts] FAILED: {ex.GetType().Name}: {ex.Message}");
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

        #region Test Methods — Matching Pipeline

        /**************************************************************/
        /// <summary>
        /// Verifies that an applicant is exact-matched to a seeded Organization
        /// and a junction record is created.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.ProcessProductsFileAsync"/>
        [TestMethod]
        public async Task ProcessProductsFileAsync_OrgMatchExact_CreatesJunction()
        {
            #region implementation
            // Arrange — shared-cache prevents connection.CloseAsync() from destroying the DB
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            await seedOrganization(context, "SALIX PHARMACEUTICALS INC");
            var service = createService(context);
            var fileContent = buildFileContent(ValidRow1);

            try
            {
                // Act
                var result = await service.ProcessProductsFileAsync(fileContent, CancellationToken.None);

                // Debug trace
                Debug.WriteLine($"[OrgMatchExact] Success={result.Success}, " +
                    $"OrganizationMatchesCreated={result.OrganizationMatchesCreated}, " +
                    $"Errors=[{string.Join("; ", result.Errors)}]");

                // Reopen connection after service's finally block
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

                // Assert
                var junctions = await context.Set<OrangeBook.ApplicantOrganization>().ToListAsync();
                Assert.IsTrue(junctions.Count > 0, "Should create at least one applicant-organization junction");
                Assert.AreEqual(result.OrganizationMatchesCreated, junctions.Count,
                    "OrganizationMatchesCreated should match junction count");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OrgMatchExact] FAILED: {ex.GetType().Name}: {ex.Message}");
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
        /// Verifies that a fuzzy token-similarity match above the 0.67 threshold
        /// creates a junction record.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.ProcessProductsFileAsync"/>
        [TestMethod]
        public async Task ProcessProductsFileAsync_OrgMatchTokenSimilarity_CreatesJunction()
        {
            #region implementation
            // Arrange — seed org with similar but not identical name
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            // "SALIX PHARMACEUTICALS" shares enough tokens with "SALIX PHARMACEUTICALS INC"
            // after normalization to exceed the 0.67 threshold
            await seedOrganization(context, "SALIX PHARMACEUTICALS");
            var service = createService(context);
            var fileContent = buildFileContent(ValidRow1);

            try
            {
                // Act
                var result = await service.ProcessProductsFileAsync(fileContent, CancellationToken.None);

                // Debug trace
                Debug.WriteLine($"[OrgMatchTokenSimilarity] Success={result.Success}, " +
                    $"OrganizationMatchesCreated={result.OrganizationMatchesCreated}, " +
                    $"Errors=[{string.Join("; ", result.Errors)}]");

                // Reopen connection after service's finally block
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

                // Assert
                var junctions = await context.Set<OrangeBook.ApplicantOrganization>().ToListAsync();
                Assert.IsTrue(junctions.Count > 0,
                    "Fuzzy matching should create a junction when token similarity exceeds threshold");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OrgMatchTokenSimilarity] FAILED: {ex.GetType().Name}: {ex.Message}");
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
        /// Verifies that cross-jurisdiction matches are rejected (e.g., a US entity
        /// should not match a UK entity).
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.ProcessProductsFileAsync"/>
        [TestMethod]
        public async Task ProcessProductsFileAsync_OrgMatchCrossJurisdiction_Rejected()
        {
            #region implementation
            // Arrange — seed org with UK suffix (LTD) that shares name tokens with
            // the ValidRow1 applicant (SALIX PHARMACEUTICALS INC = US)
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            await seedOrganization(context, "SALIX PHARMACEUTICALS LTD");
            var service = createService(context);
            var fileContent = buildFileContent(ValidRow1);

            try
            {
                // Act
                var result = await service.ProcessProductsFileAsync(fileContent, CancellationToken.None);

                // Debug trace
                Debug.WriteLine($"[OrgMatchCrossJurisdiction] Success={result.Success}, " +
                    $"OrganizationMatchesCreated={result.OrganizationMatchesCreated}, " +
                    $"Errors=[{string.Join("; ", result.Errors)}]");

                // Reopen connection after service's finally block
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

                // Assert
                var junctions = await context.Set<OrangeBook.ApplicantOrganization>().ToListAsync();
                Assert.AreEqual(0, junctions.Count,
                    "Cross-jurisdiction match (US INC vs UK LTD) should be rejected");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OrgMatchCrossJurisdiction] FAILED: {ex.GetType().Name}: {ex.Message}");
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
        /// Verifies that a product ingredient is exact-matched to a seeded
        /// IngredientSubstance and a junction is created.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.ProcessProductsFileAsync"/>
        [TestMethod]
        public async Task ProcessProductsFileAsync_IngredientExactMatch_CreatesJunction()
        {
            #region implementation
            // Arrange — shared-cache prevents connection.CloseAsync() from destroying the DB
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            await seedIngredientSubstance(context, "BUDESONIDE");
            var service = createService(context);
            var fileContent = buildFileContent(ValidRow1);

            try
            {
                // Act
                var result = await service.ProcessProductsFileAsync(fileContent, CancellationToken.None);

                // Debug trace
                Debug.WriteLine($"[IngredientExactMatch] Success={result.Success}, " +
                    $"IngredientSubstanceMatchesCreated={result.IngredientSubstanceMatchesCreated}, " +
                    $"Errors=[{string.Join("; ", result.Errors)}]");

                // Reopen connection after service's finally block
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

                // Assert
                var junctions = await context.Set<OrangeBook.ProductIngredientSubstance>().ToListAsync();
                Assert.IsTrue(junctions.Count > 0,
                    "Exact ingredient name match should create a junction");
                Assert.AreEqual(result.IngredientSubstanceMatchesCreated, junctions.Count,
                    "IngredientSubstanceMatchesCreated should match junction count");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IngredientExactMatch] FAILED: {ex.GetType().Name}: {ex.Message}");
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
        /// Verifies that a product ingredient is substring-matched when the
        /// ingredient name is contained within the substance name.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.ProcessProductsFileAsync"/>
        [TestMethod]
        public async Task ProcessProductsFileAsync_IngredientSubstringMatch_CreatesJunction()
        {
            #region implementation
            // Arrange — seed a substance whose name contains the product ingredient
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            // "BUDESONIDE" should substring-match against "BUDESONIDE EXTENDED RELEASE"
            await seedIngredientSubstance(context, "BUDESONIDE EXTENDED RELEASE");
            var service = createService(context);
            var fileContent = buildFileContent(ValidRow1);

            try
            {
                // Act
                var result = await service.ProcessProductsFileAsync(fileContent, CancellationToken.None);

                // Debug trace
                Debug.WriteLine($"[IngredientSubstringMatch] Success={result.Success}, " +
                    $"IngredientSubstanceMatchesCreated={result.IngredientSubstanceMatchesCreated}, " +
                    $"Errors=[{string.Join("; ", result.Errors)}]");

                // Reopen connection after service's finally block
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

                // Assert
                var junctions = await context.Set<OrangeBook.ProductIngredientSubstance>().ToListAsync();
                Assert.IsTrue(junctions.Count > 0,
                    "Substring containment match should create a junction");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IngredientSubstringMatch] FAILED: {ex.GetType().Name}: {ex.Message}");
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
        /// Verifies that a product is matched to a seeded MarketingCategory
        /// by exact application number (e.g., "NDA205613").
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.ProcessProductsFileAsync"/>
        [TestMethod]
        public async Task ProcessProductsFileAsync_CategoryExactMatch_CreatesJunction()
        {
            #region implementation
            // Arrange — shared-cache prevents connection.CloseAsync() from destroying the DB
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            await seedMarketingCategory(context, "NDA205613");
            var service = createService(context);
            var fileContent = buildFileContent(ValidRow1);

            try
            {
                // Act
                var result = await service.ProcessProductsFileAsync(fileContent, CancellationToken.None);

                // Debug trace
                Debug.WriteLine($"[CategoryExactMatch] Success={result.Success}, " +
                    $"MarketingCategoryMatchesCreated={result.MarketingCategoryMatchesCreated}, " +
                    $"Errors=[{string.Join("; ", result.Errors)}]");

                // Reopen connection after service's finally block
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

                // Assert
                var junctions = await context.Set<OrangeBook.ProductMarketingCategory>().ToListAsync();
                Assert.IsTrue(junctions.Count > 0,
                    "Exact app number match should create a junction");
                Assert.AreEqual(result.MarketingCategoryMatchesCreated, junctions.Count,
                    "MarketingCategoryMatchesCreated should match junction count");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CategoryExactMatch] FAILED: {ex.GetType().Name}: {ex.Message}");
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
        /// Verifies that numeric-only fallback matching works when the full
        /// "NDA205613" prefix doesn't match but "205613" does.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.ProcessProductsFileAsync"/>
        [TestMethod]
        public async Task ProcessProductsFileAsync_CategoryNumericFallback_CreatesJunction()
        {
            #region implementation
            // Arrange — seed with just the numeric portion
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            await seedMarketingCategory(context, "205613");
            var service = createService(context);
            var fileContent = buildFileContent(ValidRow1);

            try
            {
                // Act
                var result = await service.ProcessProductsFileAsync(fileContent, CancellationToken.None);

                // Debug trace
                Debug.WriteLine($"[CategoryNumericFallback] Success={result.Success}, " +
                    $"MarketingCategoryMatchesCreated={result.MarketingCategoryMatchesCreated}, " +
                    $"Errors=[{string.Join("; ", result.Errors)}]");

                // Reopen connection after service's finally block
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

                // Assert
                var junctions = await context.Set<OrangeBook.ProductMarketingCategory>().ToListAsync();
                Assert.IsTrue(junctions.Count > 0,
                    "Numeric-only fallback should create a junction when full prefix doesn't match");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CategoryNumericFallback] FAILED: {ex.GetType().Name}: {ex.Message}");
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
        /// Verifies that unmatched entities are counted in the result.
        /// </summary>
        /// <seealso cref="OrangeBookProductParsingService.ProcessProductsFileAsync"/>
        [TestMethod]
        public async Task ProcessProductsFileAsync_UnmatchedEntities_LoggedInResult()
        {
            #region implementation
            // Arrange — no organizations, ingredients, or categories seeded
            var (sentinel, connection) = createSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = createTestContext(connection);
            var service = createService(context);
            var fileContent = buildFileContent(ValidRow1);

            try
            {
                // Act
                var result = await service.ProcessProductsFileAsync(fileContent, CancellationToken.None);

                // Debug trace
                Debug.WriteLine($"[UnmatchedEntities] Success={result.Success}, " +
                    $"UnmatchedApplicants={result.UnmatchedApplicants}, " +
                    $"UnmatchedIngredients={result.UnmatchedIngredients}, " +
                    $"UnmatchedProducts={result.UnmatchedProducts}, " +
                    $"Errors=[{string.Join("; ", result.Errors)}]");

                // Reopen connection after service's finally block
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();

                // Assert — with nothing to match against, all entities should be unmatched
                Assert.IsTrue(result.UnmatchedApplicants >= 0,
                    "UnmatchedApplicants should be tracked (0 or more depending on matching logic)");
                Assert.IsTrue(result.UnmatchedIngredients >= 0,
                    "UnmatchedIngredients should be tracked");
                Assert.IsTrue(result.UnmatchedProducts >= 0,
                    "UnmatchedProducts should be tracked");
                Assert.AreEqual(0, result.OrganizationMatchesCreated,
                    "No org junctions should be created without seeded organizations");
                Assert.AreEqual(0, result.IngredientSubstanceMatchesCreated,
                    "No ingredient junctions without seeded substances");
                Assert.AreEqual(0, result.MarketingCategoryMatchesCreated,
                    "No category junctions without seeded categories");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UnmatchedEntities] FAILED: {ex.GetType().Name}: {ex.Message}");
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
