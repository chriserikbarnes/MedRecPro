using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Shared test infrastructure for all DtoLabelAccess test classes.
    /// Provides SQLite database setup, view backing table creation,
    /// entity seeding, cache management, and test constants.
    /// </summary>
    /// <remarks>
    /// Uses shared-cache named SQLite in-memory databases with sentinel
    /// connections following the pattern established in
    /// <see cref="OrangeBookProductParsingServiceTests"/>.
    ///
    /// View entities (LabelView.*) cannot be seeded via EF Core
    /// (keyless entities reject Add/SaveChanges), so raw SQL INSERT
    /// statements are used via SqliteCommand.
    /// </remarks>
    /// <seealso cref="MedRecPro.DataAccess.DtoLabelAccess"/>
    /// <seealso cref="LabelView"/>
    /// <seealso cref="Label"/>
    public static class DtoLabelAccessTestHelper
    {
        #region Test Constants

        /**************************************************************/
        /// <summary>
        /// Encryption secret used for ID encryption in DTOs.
        /// </summary>
        public const string TestPkSecret = "TestEncryptionSecretKey12345!@#";

        /**************************************************************/
        /// <summary>
        /// Default test document GUID for consistent test data.
        /// </summary>
        public static readonly Guid TestDocumentGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");

        /**************************************************************/
        /// <summary>
        /// Second test document GUID for multi-document tests.
        /// </summary>
        public static readonly Guid TestDocumentGuid2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        /**************************************************************/
        /// <summary>
        /// Third test document GUID for pagination tests.
        /// </summary>
        public static readonly Guid TestDocumentGuid3 = Guid.Parse("33333333-3333-3333-3333-333333333333");

        /**************************************************************/
        /// <summary>
        /// Default test set GUID for consistent test data.
        /// </summary>
        public static readonly Guid TestSetGuid = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");

        /**************************************************************/
        /// <summary>
        /// Second test set GUID for multi-set tests.
        /// </summary>
        public static readonly Guid TestSetGuid2 = Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB");

        /**************************************************************/
        /// <summary>
        /// Default test section GUID.
        /// </summary>
        public static readonly Guid TestSectionGuid = Guid.Parse("CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC");

        #endregion Test Constants

        #region Database Setup

        /**************************************************************/
        /// <summary>
        /// Creates a shared named in-memory SQLite database that survives connection close/reopen.
        /// The sentinel connection keeps the DB alive for the test's lifetime.
        /// </summary>
        /// <returns>
        /// A tuple of (sentinelConnection, serviceConnection).
        /// The sentinel must stay open for the DB's lifetime. Caller must dispose both.
        /// </returns>
        /// <seealso cref="CreateTestContext"/>
        public static (SqliteConnection sentinel, SqliteConnection connection) CreateSharedMemoryDb()
        {
            #region implementation

            var dbName = $"file:test_{Guid.NewGuid():N}?mode=memory&cache=shared";
            var connStr = $"DataSource={dbName}";

            var sentinel = new SqliteConnection(connStr);
            sentinel.Open();

            var connection = new SqliteConnection(connStr);
            connection.Open();

            return (sentinel, connection);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an ApplicationDbContext backed by the given SQLite connection,
        /// with patched DDL for SQLite compatibility and view backing tables
        /// created via reflection on LabelView nested types.
        /// </summary>
        /// <param name="connection">Open SQLite connection (caller must dispose).</param>
        /// <returns>A configured <see cref="ApplicationDbContext"/>.</returns>
        /// <remarks>
        /// 1. Generates DDL from EF Core for Label.* table entities
        /// 2. Patches SQL Server types for SQLite (nvarchar→TEXT, decimal→REAL, etc.)
        /// 3. Creates backing tables for LabelView.* entities via reflection
        /// 4. Executes all DDL statements individually (skips failures gracefully)
        /// </remarks>
        /// <seealso cref="CreateSharedMemoryDb"/>
        public static ApplicationDbContext CreateTestContext(SqliteConnection connection)
        {
            #region implementation

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new ApplicationDbContext(options);

            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();

            // Generate and patch DDL for Label.* table entities
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
                    // Skip unsupported SQL Server constructs
                }
            }

            // Create backing tables for LabelView.* entities via reflection
            createViewBackingTables(connection);

            return context;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates SQLite backing tables for all LabelView nested types.
        /// Enumerates nested public classes from <see cref="LabelView"/>,
        /// reads their <see cref="TableAttribute"/> for the table name,
        /// and generates CREATE TABLE IF NOT EXISTS from their public properties.
        /// </summary>
        /// <param name="connection">Open SQLite connection.</param>
        private static void createViewBackingTables(SqliteConnection connection)
        {
            #region implementation

            var labelViewType = typeof(LabelView);
            var nestedTypes = labelViewType.GetNestedTypes(BindingFlags.Public)
                .Where(t => t.IsClass && !t.IsAbstract);

            foreach (var entityType in nestedTypes)
            {
                var tableAttr = entityType.GetCustomAttribute<TableAttribute>();
                var tableName = tableAttr?.Name ?? entityType.Name;

                var columns = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.CanWrite)
                    .Select(p => $"\"{p.Name}\" {mapCSharpTypeToSqlite(p.PropertyType)}")
                    .ToList();

                if (columns.Count == 0) continue;

                var createSql = $"CREATE TABLE IF NOT EXISTS \"{tableName}\" ({string.Join(", ", columns)})";

                try
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = createSql;
                    cmd.ExecuteNonQuery();
                }
                catch (SqliteException)
                {
                    // Skip if table already exists or creation fails
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Maps a C# property type to its SQLite column type equivalent.
        /// </summary>
        /// <param name="type">The C# property type.</param>
        /// <returns>SQLite column type string.</returns>
        private static string mapCSharpTypeToSqlite(Type type)
        {
            #region implementation

            // Unwrap Nullable<T>
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(bool))
                return "INTEGER";
            if (underlying == typeof(decimal) || underlying == typeof(double) || underlying == typeof(float))
                return "REAL";
            if (underlying == typeof(byte[]))
                return "BLOB";
            // string, Guid, DateTime, DateOnly, etc. all stored as TEXT
            return "TEXT";

            #endregion
        }

        #endregion Database Setup

        #region Cache Management

        /**************************************************************/
        /// <summary>
        /// Clears the PerformanceHelper managed cache to prevent cross-test pollution.
        /// Must be called in [TestInitialize] of every test class.
        /// </summary>
        public static void ClearCache()
        {
            #region implementation

            PerformanceHelper.ResetManagedCache();

            #endregion
        }

        #endregion Cache Management

        #region Logger

        /**************************************************************/
        /// <summary>
        /// Creates a mock ILogger instance for test use.
        /// </summary>
        /// <returns>A mocked <see cref="ILogger"/> instance.</returns>
        public static ILogger CreateTestLogger()
        {
            #region implementation

            return new Mock<ILogger>().Object;

            #endregion
        }

        #endregion Logger

        #region Label Entity Seed Methods

        /**************************************************************/
        /// <summary>
        /// Seeds a <see cref="Label.Document"/> entity into the test database.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="documentGuid">The document GUID.</param>
        /// <param name="setGuid">The set GUID.</param>
        /// <param name="title">Optional document title.</param>
        /// <param name="versionNumber">Optional version number.</param>
        /// <returns>The generated DocumentID.</returns>
        public static async Task<int> SeedDocumentAsync(
            ApplicationDbContext context,
            Guid documentGuid,
            Guid setGuid,
            string title = "Test Document",
            int versionNumber = 1)
        {
            #region implementation

            var doc = new Label.Document
            {
                DocumentGUID = documentGuid,
                SetGUID = setGuid,
                Title = title,
                VersionNumber = versionNumber,
                DocumentCode = "34391-3",
                DocumentCodeSystem = "2.16.840.1.113883.6.1",
                DocumentCodeSystemName = "LOINC",
                DocumentDisplayName = "HUMAN PRESCRIPTION DRUG LABEL",
                EffectiveTime = DateTime.UtcNow
            };

            context.Set<Label.Document>().Add(doc);
            await context.SaveChangesAsync();
            return doc.DocumentID!.Value;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a <see cref="Label.StructuredBody"/> entity into the test database.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="documentId">FK to Document.</param>
        /// <returns>The generated StructuredBodyID.</returns>
        public static async Task<int> SeedStructuredBodyAsync(
            ApplicationDbContext context,
            int documentId)
        {
            #region implementation

            var body = new Label.StructuredBody
            {
                DocumentID = documentId
            };

            context.Set<Label.StructuredBody>().Add(body);
            await context.SaveChangesAsync();
            return body.StructuredBodyID!.Value;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a <see cref="Label.Section"/> entity into the test database.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="structuredBodyId">FK to StructuredBody.</param>
        /// <param name="documentId">FK to Document.</param>
        /// <param name="sectionCode">LOINC section code.</param>
        /// <param name="title">Section title.</param>
        /// <returns>The generated SectionID.</returns>
        public static async Task<int> SeedSectionAsync(
            ApplicationDbContext context,
            int structuredBodyId,
            int documentId,
            string sectionCode = "34067-9",
            string title = "INDICATIONS AND USAGE")
        {
            #region implementation

            var section = new Label.Section
            {
                StructuredBodyID = structuredBodyId,
                DocumentID = documentId,
                SectionCode = sectionCode,
                SectionCodeSystem = "2.16.840.1.113883.6.1",
                SectionCodeSystemName = "LOINC",
                SectionDisplayName = title,
                Title = title,
                SectionGUID = Guid.NewGuid()
            };

            context.Set<Label.Section>().Add(section);
            await context.SaveChangesAsync();
            return section.SectionID!.Value;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a <see cref="Label.SectionTextContent"/> entity into the test database.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="sectionId">FK to Section.</param>
        /// <param name="contentText">The section text content.</param>
        /// <param name="sequenceNumber">Order within the section.</param>
        /// <returns>The generated SectionTextContentID.</returns>
        public static async Task<int> SeedSectionTextContentAsync(
            ApplicationDbContext context,
            int sectionId,
            string contentText = "Test section content text.",
            int sequenceNumber = 1)
        {
            #region implementation

            var content = new Label.SectionTextContent
            {
                SectionID = sectionId,
                ContentText = contentText,
                SequenceNumber = sequenceNumber,
                ContentType = "Paragraph"
            };

            context.Set<Label.SectionTextContent>().Add(content);
            await context.SaveChangesAsync();
            return content.SectionTextContentID!.Value;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a <see cref="Label.Product"/> entity into the test database.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="sectionId">FK to Section.</param>
        /// <param name="productName">The product name.</param>
        /// <returns>The generated ProductID.</returns>
        public static async Task<int> SeedProductAsync(
            ApplicationDbContext context,
            int sectionId,
            string productName = "ASPIRIN")
        {
            #region implementation

            var product = new Label.Product
            {
                SectionID = sectionId,
                ProductName = productName,
                FormCode = "C42998",
                FormDisplayName = "TABLET"
            };

            context.Set<Label.Product>().Add(product);
            await context.SaveChangesAsync();
            return product.ProductID!.Value;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a <see cref="Label.IngredientSubstance"/> entity into the test database.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="substanceName">The substance name.</param>
        /// <param name="unii">The UNII code.</param>
        /// <returns>The generated IngredientSubstanceID.</returns>
        public static async Task<int> SeedIngredientSubstanceAsync(
            ApplicationDbContext context,
            string substanceName = "ASPIRIN",
            string unii = "R16CO5Y76E")
        {
            #region implementation

            var substance = new Label.IngredientSubstance
            {
                SubstanceName = substanceName,
                UNII = unii
            };

            context.Set<Label.IngredientSubstance>().Add(substance);
            await context.SaveChangesAsync();
            return substance.IngredientSubstanceID!.Value;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a <see cref="Label.Ingredient"/> entity into the test database.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="productId">FK to Product.</param>
        /// <param name="ingredientSubstanceId">FK to IngredientSubstance.</param>
        /// <param name="classCode">Ingredient class code (ACTIB, ACTIM, IACT, etc.).</param>
        /// <returns>The generated IngredientID.</returns>
        public static async Task<int> SeedIngredientAsync(
            ApplicationDbContext context,
            int productId,
            int ingredientSubstanceId,
            string classCode = "ACTIB")
        {
            #region implementation

            var ingredient = new Label.Ingredient
            {
                ProductID = productId,
                IngredientSubstanceID = ingredientSubstanceId,
                ClassCode = classCode
            };

            context.Set<Label.Ingredient>().Add(ingredient);
            await context.SaveChangesAsync();
            return ingredient.IngredientID!.Value;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a <see cref="Label.PackagingLevel"/> entity into the test database.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="productId">FK to Product.</param>
        /// <returns>The generated PackagingLevelID.</returns>
        public static async Task<int> SeedPackagingLevelAsync(
            ApplicationDbContext context,
            int productId)
        {
            #region implementation

            var packaging = new Label.PackagingLevel
            {
                ProductID = productId,
                QuantityNumerator = 100,
                QuantityNumeratorUnit = "1",
                PackageFormCode = "C43169",
                PackageFormDisplayName = "BOTTLE"
            };

            context.Set<Label.PackagingLevel>().Add(packaging);
            await context.SaveChangesAsync();
            return packaging.PackagingLevelID!.Value;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a <see cref="Label.PackageIdentifier"/> entity into the test database.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="packagingLevelId">FK to PackagingLevel.</param>
        /// <param name="identifierValue">The NDC or other package code.</param>
        /// <param name="identifierType">The identifier type (e.g., NDCPackage).</param>
        /// <returns>The generated PackageIdentifierID.</returns>
        public static async Task<int> SeedPackageIdentifierAsync(
            ApplicationDbContext context,
            int packagingLevelId,
            string identifierValue = "12345-678-90",
            string identifierType = "NDCPackage")
        {
            #region implementation

            var identifier = new Label.PackageIdentifier
            {
                PackagingLevelID = packagingLevelId,
                IdentifierValue = identifierValue,
                IdentifierType = identifierType
            };

            context.Set<Label.PackageIdentifier>().Add(identifier);
            await context.SaveChangesAsync();
            return identifier.PackageIdentifierID!.Value;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a <see cref="Label.DocumentAuthor"/> entity into the test database.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="documentId">FK to Document.</param>
        /// <param name="organizationId">FK to Organization.</param>
        /// <returns>The generated DocumentAuthorID.</returns>
        public static async Task<int> SeedDocumentAuthorAsync(
            ApplicationDbContext context,
            int documentId,
            int? organizationId = null)
        {
            #region implementation

            var author = new Label.DocumentAuthor
            {
                DocumentID = documentId,
                OrganizationID = organizationId,
                AuthorType = "Labeler"
            };

            context.Set<Label.DocumentAuthor>().Add(author);
            await context.SaveChangesAsync();
            return author.DocumentAuthorID!.Value;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a <see cref="Label.Organization"/> entity into the test database.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="organizationName">The organization name.</param>
        /// <returns>The generated OrganizationID.</returns>
        public static async Task<int> SeedOrganizationAsync(
            ApplicationDbContext context,
            string organizationName = "TEST PHARMACEUTICALS INC")
        {
            #region implementation

            var org = new Label.Organization
            {
                OrganizationName = organizationName
            };

            context.Set<Label.Organization>().Add(org);
            await context.SaveChangesAsync();
            return org.OrganizationID!.Value;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a <see cref="Label.MarketingCategory"/> entity into the test database.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="productId">FK to Product.</param>
        /// <param name="applicationNumber">Application number (e.g., NDA014526).</param>
        /// <param name="categoryCode">Marketing category code (e.g., NDA).</param>
        /// <returns>The generated MarketingCategoryID.</returns>
        public static async Task<int> SeedMarketingCategoryAsync(
            ApplicationDbContext context,
            int productId,
            string applicationNumber = "NDA014526",
            string categoryCode = "NDA")
        {
            #region implementation

            var category = new Label.MarketingCategory
            {
                ProductID = productId,
                ApplicationOrMonographIDValue = applicationNumber,
                CategoryCode = categoryCode,
                CategoryDisplayName = categoryCode,
                ApprovalDate = new DateTime(2020, 1, 15)
            };

            context.Set<Label.MarketingCategory>().Add(category);
            await context.SaveChangesAsync();
            return category.MarketingCategoryID!.Value;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a complete document hierarchy: Document → StructuredBody → Section → Product.
        /// Convenient for tests that need a minimal but complete entity graph.
        /// </summary>
        /// <param name="context">The test database context.</param>
        /// <param name="documentGuid">The document GUID.</param>
        /// <param name="setGuid">The set GUID.</param>
        /// <param name="productName">Product name to seed.</param>
        /// <returns>Tuple of (DocumentID, StructuredBodyID, SectionID, ProductID).</returns>
        public static async Task<(int DocumentID, int StructuredBodyID, int SectionID, int ProductID)>
            SeedFullDocumentHierarchyAsync(
                ApplicationDbContext context,
                Guid documentGuid,
                Guid setGuid,
                string productName = "ASPIRIN")
        {
            #region implementation

            var docId = await SeedDocumentAsync(context, documentGuid, setGuid);
            var bodyId = await SeedStructuredBodyAsync(context, docId);
            var sectionId = await SeedSectionAsync(context, bodyId, docId);
            var productId = await SeedProductAsync(context, sectionId, productName);

            return (docId, bodyId, sectionId, productId);

            #endregion
        }

        #endregion Label Entity Seed Methods

        #region LabelView Raw SQL Seed Methods

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_ProductsByApplicationNumber backing table via raw SQL.
        /// </summary>
        public static void SeedProductsByApplicationNumberView(
            SqliteConnection connection,
            string applicationNumber = "NDA014526",
            string productName = "ASPIRIN",
            int productId = 1,
            int documentId = 1,
            Guid? documentGuid = null,
            Guid? setGuid = null,
            string? labelerName = "TEST PHARMA INC",
            string? marketingCategoryCode = "NDA",
            string? marketingCategoryName = "New Drug Application")
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_ProductsByApplicationNumber""
                (ApplicationNumber, MarketingCategoryCode, MarketingCategoryName,
                 ProductID, ProductName, DocumentID, DocumentGUID, SetGUID, LabelerName, VersionNumber)
                VALUES ($appNum, $catCode, $catName, $prodId, $prodName, $docId, $docGuid, $setGuid, $labeler, 1)";
            cmd.Parameters.AddWithValue("$appNum", applicationNumber);
            cmd.Parameters.AddWithValue("$catCode", (object?)marketingCategoryCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$catName", (object?)marketingCategoryName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$prodId", productId);
            cmd.Parameters.AddWithValue("$prodName", productName);
            cmd.Parameters.AddWithValue("$docId", documentId);
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$setGuid", (setGuid ?? TestSetGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$labeler", (object?)labelerName ?? DBNull.Value);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_ApplicationNumberSummary backing table via raw SQL.
        /// </summary>
        public static void SeedApplicationNumberSummaryView(
            SqliteConnection connection,
            string applicationNumber = "NDA014526",
            string marketingCategoryCode = "NDA",
            string marketingCategoryName = "New Drug Application",
            int productCount = 5,
            int documentCount = 3,
            int labelSetCount = 2)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_ApplicationNumberSummary""
                (ApplicationNumber, MarketingCategoryCode, MarketingCategoryName,
                 ProductCount, DocumentCount, LabelSetCount,
                 EarliestApprovalDate, LatestApprovalDate, MostRecentLabelDate)
                VALUES ($appNum, $catCode, $catName, $prodCount, $docCount, $setCount,
                        '2020-01-15', '2024-06-01', '2024-06-01')";
            cmd.Parameters.AddWithValue("$appNum", applicationNumber);
            cmd.Parameters.AddWithValue("$catCode", marketingCategoryCode);
            cmd.Parameters.AddWithValue("$catName", marketingCategoryName);
            cmd.Parameters.AddWithValue("$prodCount", productCount);
            cmd.Parameters.AddWithValue("$docCount", documentCount);
            cmd.Parameters.AddWithValue("$setCount", labelSetCount);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_ProductsByPharmacologicClass backing table via raw SQL.
        /// </summary>
        public static void SeedProductsByPharmacologicClassView(
            SqliteConnection connection,
            string pharmClassName = "Cyclooxygenase Inhibitors",
            int productId = 1,
            string productName = "ASPIRIN",
            int documentId = 1,
            Guid? documentGuid = null,
            Guid? setGuid = null)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_ProductsByPharmacologicClass""
                (PharmacologicClassID, PharmClassCode, PharmClassName,
                 ProductID, ProductName, DocumentID, DocumentGUID, SetGUID, VersionNumber)
                VALUES (1, 'N02BA01', $className, $prodId, $prodName, $docId, $docGuid, $setGuid, 1)";
            cmd.Parameters.AddWithValue("$className", pharmClassName);
            cmd.Parameters.AddWithValue("$prodId", productId);
            cmd.Parameters.AddWithValue("$prodName", productName);
            cmd.Parameters.AddWithValue("$docId", documentId);
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$setGuid", (setGuid ?? TestSetGuid).ToString("D").ToUpper());
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_PharmacologicClassHierarchy backing table via raw SQL.
        /// </summary>
        public static void SeedPharmacologicClassHierarchyView(
            SqliteConnection connection,
            string childClassName = "Cyclooxygenase Inhibitors",
            string parentClassName = "Anti-Inflammatory Agents",
            int childClassId = 1,
            int parentClassId = 2)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_PharmacologicClassHierarchy""
                (ChildClassID, ChildClassCode, ChildClassName,
                 ParentClassID, ParentClassCode, ParentClassName, PharmClassHierarchyID)
                VALUES ($childId, 'N02BA01', $childName, $parentId, 'M01A', $parentName, NULL)";
            cmd.Parameters.AddWithValue("$childName", childClassName);
            cmd.Parameters.AddWithValue("$parentName", parentClassName);
            cmd.Parameters.AddWithValue("$childId", childClassId);
            cmd.Parameters.AddWithValue("$parentId", parentClassId);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_PharmacologicClassSummary backing table via raw SQL.
        /// </summary>
        public static void SeedPharmacologicClassSummaryView(
            SqliteConnection connection,
            string pharmClassName = "Cyclooxygenase Inhibitors",
            int productCount = 10,
            int documentCount = 5)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_PharmacologicClassSummary""
                (PharmacologicClassID, PharmClassCode, PharmClassName,
                 LinkedSubstanceCount, ProductCount, DocumentCount)
                VALUES (1, 'N02BA01', $className, 3, $prodCount, $docCount)";
            cmd.Parameters.AddWithValue("$className", pharmClassName);
            cmd.Parameters.AddWithValue("$prodCount", productCount);
            cmd.Parameters.AddWithValue("$docCount", documentCount);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_IngredientActiveSummary backing table via raw SQL.
        /// </summary>
        public static void SeedIngredientActiveSummaryView(
            SqliteConnection connection,
            int ingredientSubstanceId = 1,
            string substanceName = "ASPIRIN",
            string unii = "R16CO5Y76E",
            int productCount = 10,
            int documentCount = 5,
            int labelerCount = 3)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_IngredientActiveSummary""
                (IngredientSubstanceID, UNII, SubstanceName, IngredientType,
                 ProductCount, DocumentCount, LabelerCount)
                VALUES ($substId, $unii, $name, 'Active', $prodCount, $docCount, $labCount)";
            cmd.Parameters.AddWithValue("$substId", ingredientSubstanceId);
            cmd.Parameters.AddWithValue("$unii", unii);
            cmd.Parameters.AddWithValue("$name", substanceName);
            cmd.Parameters.AddWithValue("$prodCount", productCount);
            cmd.Parameters.AddWithValue("$docCount", documentCount);
            cmd.Parameters.AddWithValue("$labCount", labelerCount);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_IngredientInactiveSummary backing table via raw SQL.
        /// </summary>
        public static void SeedIngredientInactiveSummaryView(
            SqliteConnection connection,
            int ingredientSubstanceId = 1,
            string substanceName = "STARCH",
            string unii = "O8232NY3SJ",
            int productCount = 5,
            int documentCount = 3,
            int labelerCount = 2)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_IngredientInactiveSummary""
                (IngredientSubstanceID, UNII, SubstanceName, IngredientType,
                 ProductCount, DocumentCount, LabelerCount)
                VALUES ($substId, $unii, $name, 'Inactive', $prodCount, $docCount, $labCount)";
            cmd.Parameters.AddWithValue("$substId", ingredientSubstanceId);
            cmd.Parameters.AddWithValue("$unii", unii);
            cmd.Parameters.AddWithValue("$name", substanceName);
            cmd.Parameters.AddWithValue("$prodCount", productCount);
            cmd.Parameters.AddWithValue("$docCount", documentCount);
            cmd.Parameters.AddWithValue("$labCount", labelerCount);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_ProductsByIngredient backing table via raw SQL.
        /// </summary>
        public static void SeedProductsByIngredientView(
            SqliteConnection connection,
            string substanceName = "ASPIRIN",
            string unii = "R16CO5Y76E",
            int productId = 1,
            string productName = "ASPIRIN",
            int documentId = 1,
            Guid? documentGuid = null,
            Guid? setGuid = null)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_ProductsByIngredient""
                (IngredientSubstanceID, UNII, SubstanceName, IngredientType,
                 IngredientID, IngredientClassCode,
                 ProductID, ProductName, DocumentID, DocumentGUID, SetGUID, VersionNumber)
                VALUES (1, $unii, $substName, 'Active',
                        1, 'ACTIB',
                        $prodId, $prodName, $docId, $docGuid, $setGuid, 1)";
            cmd.Parameters.AddWithValue("$unii", unii);
            cmd.Parameters.AddWithValue("$substName", substanceName);
            cmd.Parameters.AddWithValue("$prodId", productId);
            cmd.Parameters.AddWithValue("$prodName", productName);
            cmd.Parameters.AddWithValue("$docId", documentId);
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$setGuid", (setGuid ?? TestSetGuid).ToString("D").ToUpper());
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_IngredientSummary backing table via raw SQL.
        /// </summary>
        public static void SeedIngredientSummaryView(
            SqliteConnection connection,
            string substanceName = "ASPIRIN",
            string unii = "R16CO5Y76E",
            int productCount = 10)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_IngredientSummary""
                (IngredientSubstanceID, UNII, SubstanceName, IngredientType,
                 ProductCount, DocumentCount, LabelerCount)
                VALUES (1, $unii, $name, 'Active', $prodCount, 5, 3)";
            cmd.Parameters.AddWithValue("$unii", unii);
            cmd.Parameters.AddWithValue("$name", substanceName);
            cmd.Parameters.AddWithValue("$prodCount", productCount);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_Ingredients backing table via raw SQL.
        /// </summary>
        public static void SeedIngredientView(
            SqliteConnection connection,
            string substanceName = "ASPIRIN",
            string unii = "R16CO5Y76E",
            string classCode = "ACTIB",
            string productName = "ASPIRIN",
            string applicationNumber = "NDA014526",
            string applicationType = "NDA",
            Guid? documentGuid = null,
            Guid? setGuid = null)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_Ingredients""
                (DocumentGUID, SetGUID, SectionGUID, IngredientID, ProductID,
                 IngredientSubstanceID, MarketingCategoryID, SectionID, DocumentID,
                 ClassCode, ProductName, SubstanceName, UNII,
                 ApplicationType, ApplicationNumber)
                VALUES ($docGuid, $setGuid, $secGuid, 1, 1,
                        1, 1, 1, 1,
                        $classCode, $prodName, $substName, $unii,
                        $appType, $appNum)";
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$setGuid", (setGuid ?? TestSetGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$secGuid", TestSectionGuid.ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$classCode", classCode);
            cmd.Parameters.AddWithValue("$prodName", productName);
            cmd.Parameters.AddWithValue("$substName", substanceName);
            cmd.Parameters.AddWithValue("$unii", unii);
            cmd.Parameters.AddWithValue("$appType", applicationType);
            cmd.Parameters.AddWithValue("$appNum", applicationNumber);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_ActiveIngredients backing table via raw SQL.
        /// </summary>
        public static void SeedActiveIngredientView(
            SqliteConnection connection,
            string substanceName = "ASPIRIN",
            string unii = "R16CO5Y76E",
            string productName = "ASPIRIN",
            string applicationNumber = "NDA014526",
            string applicationType = "NDA",
            Guid? documentGuid = null,
            Guid? setGuid = null)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_ActiveIngredients""
                (DocumentGUID, SetGUID, SectionGUID, IngredientID, ProductID,
                 IngredientSubstanceID, MarketingCategoryID, SectionID, DocumentID,
                 ClassCode, ProductName, SubstanceName, UNII,
                 ApplicationType, ApplicationNumber)
                VALUES ($docGuid, $setGuid, $secGuid, 1, 1,
                        1, 1, 1, 1,
                        'ACTIB', $prodName, $substName, $unii,
                        $appType, $appNum)";
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$setGuid", (setGuid ?? TestSetGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$secGuid", TestSectionGuid.ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$prodName", productName);
            cmd.Parameters.AddWithValue("$substName", substanceName);
            cmd.Parameters.AddWithValue("$unii", unii);
            cmd.Parameters.AddWithValue("$appType", applicationType);
            cmd.Parameters.AddWithValue("$appNum", applicationNumber);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_InactiveIngredients backing table via raw SQL.
        /// </summary>
        public static void SeedInactiveIngredientView(
            SqliteConnection connection,
            string substanceName = "STARCH",
            string unii = "O8232NY3SJ",
            string productName = "ASPIRIN",
            string applicationNumber = "NDA014526",
            Guid? documentGuid = null)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_InactiveIngredients""
                (DocumentGUID, SetGUID, SectionGUID, IngredientID, ProductID,
                 IngredientSubstanceID, MarketingCategoryID, SectionID, DocumentID,
                 ClassCode, ProductName, SubstanceName, UNII,
                 ApplicationType, ApplicationNumber)
                VALUES ($docGuid, $setGuid, $secGuid, 2, 1,
                        2, 1, 1, 1,
                        'IACT', $prodName, $substName, $unii,
                        'NDA', $appNum)";
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$setGuid", TestSetGuid.ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$secGuid", TestSectionGuid.ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$prodName", productName);
            cmd.Parameters.AddWithValue("$substName", substanceName);
            cmd.Parameters.AddWithValue("$unii", unii);
            cmd.Parameters.AddWithValue("$appNum", applicationNumber);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_ProductsByNDC backing table via raw SQL.
        /// </summary>
        public static void SeedProductsByNDCView(
            SqliteConnection connection,
            string productCode = "12345-678",
            string productName = "ASPIRIN",
            int productId = 1,
            int documentId = 1,
            Guid? documentGuid = null,
            Guid? setGuid = null)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_ProductsByNDC""
                (ProductIdentifierID, ProductCode, CodeType, ProductID, ProductName,
                 DocumentID, DocumentGUID, SetGUID, VersionNumber)
                VALUES (1, $code, 'NDC', $prodId, $prodName,
                        $docId, $docGuid, $setGuid, 1)";
            cmd.Parameters.AddWithValue("$code", productCode);
            cmd.Parameters.AddWithValue("$prodId", productId);
            cmd.Parameters.AddWithValue("$prodName", productName);
            cmd.Parameters.AddWithValue("$docId", documentId);
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$setGuid", (setGuid ?? TestSetGuid).ToString("D").ToUpper());
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_PackageByNDC backing table via raw SQL.
        /// </summary>
        public static void SeedPackageByNDCView(
            SqliteConnection connection,
            string packageCode = "12345-678-90",
            string productName = "ASPIRIN",
            int productId = 1,
            int documentId = 1,
            Guid? documentGuid = null,
            Guid? setGuid = null)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_PackageByNDC""
                (PackageIdentifierID, PackageCode, CodeType, PackagingLevelID,
                 ProductID, ProductName, DocumentID, DocumentGUID, SetGUID)
                VALUES (1, $code, 'NDCPackage', 1,
                        $prodId, $prodName, $docId, $docGuid, $setGuid)";
            cmd.Parameters.AddWithValue("$code", packageCode);
            cmd.Parameters.AddWithValue("$prodId", productId);
            cmd.Parameters.AddWithValue("$prodName", productName);
            cmd.Parameters.AddWithValue("$docId", documentId);
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$setGuid", (setGuid ?? TestSetGuid).ToString("D").ToUpper());
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_ProductsByLabeler backing table via raw SQL.
        /// </summary>
        public static void SeedProductsByLabelerView(
            SqliteConnection connection,
            string labelerName = "TEST PHARMA INC",
            int productId = 1,
            string productName = "ASPIRIN",
            int documentId = 1,
            Guid? documentGuid = null,
            Guid? setGuid = null)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_ProductsByLabeler""
                (LabelerOrgID, LabelerName, ProductID, ProductName,
                 DocumentID, DocumentGUID, SetGUID, VersionNumber)
                VALUES (1, $labeler, $prodId, $prodName,
                        $docId, $docGuid, $setGuid, 1)";
            cmd.Parameters.AddWithValue("$labeler", labelerName);
            cmd.Parameters.AddWithValue("$prodId", productId);
            cmd.Parameters.AddWithValue("$prodName", productName);
            cmd.Parameters.AddWithValue("$docId", documentId);
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$setGuid", (setGuid ?? TestSetGuid).ToString("D").ToUpper());
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_LabelerSummary backing table via raw SQL.
        /// </summary>
        public static void SeedLabelerSummaryView(
            SqliteConnection connection,
            string labelerName = "TEST PHARMA INC",
            int productCount = 10,
            int documentCount = 5)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_LabelerSummary""
                (LabelerOrgID, LabelerName, ProductCount, DocumentCount, LabelSetCount,
                 EarliestLabelDate, MostRecentLabelDate)
                VALUES (1, $labeler, $prodCount, $docCount, 2,
                        '2020-01-15', '2024-06-01')";
            cmd.Parameters.AddWithValue("$labeler", labelerName);
            cmd.Parameters.AddWithValue("$prodCount", productCount);
            cmd.Parameters.AddWithValue("$docCount", documentCount);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_DocumentNavigation backing table via raw SQL.
        /// </summary>
        public static void SeedDocumentNavigationView(
            SqliteConnection connection,
            int documentId = 1,
            Guid? documentGuid = null,
            Guid? setGuid = null,
            int versionNumber = 1,
            string documentTitle = "Test Document",
            int isLatestVersion = 1)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_DocumentNavigation""
                (DocumentID, DocumentGUID, SetGUID, VersionNumber, DocumentCode,
                 DocumentType, DocumentTitle, EffectiveDate,
                 ProductCount, TotalVersions, IsLatestVersion)
                VALUES ($docId, $docGuid, $setGuid, $ver, '34391-3',
                        'HUMAN PRESCRIPTION DRUG LABEL', $title, '2024-01-15',
                        1, 1, $isLatest)";
            cmd.Parameters.AddWithValue("$docId", documentId);
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$setGuid", (setGuid ?? TestSetGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$ver", versionNumber);
            cmd.Parameters.AddWithValue("$title", documentTitle);
            cmd.Parameters.AddWithValue("$isLatest", isLatestVersion);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_DocumentVersionHistory backing table via raw SQL.
        /// </summary>
        public static void SeedDocumentVersionHistoryView(
            SqliteConnection connection,
            Guid? setGuid = null,
            int documentId = 1,
            Guid? documentGuid = null,
            int versionNumber = 1,
            string documentTitle = "Test Document")
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_DocumentVersionHistory""
                (SetGUID, DocumentID, DocumentGUID, VersionNumber, DocumentTitle,
                 EffectiveDate, DocumentCode, DocumentType, LabelerName)
                VALUES ($setGuid, $docId, $docGuid, $ver, $title,
                        '2024-01-15', '34391-3', 'HUMAN PRESCRIPTION DRUG LABEL', 'TEST PHARMA')";
            cmd.Parameters.AddWithValue("$setGuid", (setGuid ?? TestSetGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$docId", documentId);
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$ver", versionNumber);
            cmd.Parameters.AddWithValue("$title", documentTitle);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_SectionNavigation backing table via raw SQL.
        /// </summary>
        public static void SeedSectionNavigationView(
            SqliteConnection connection,
            string sectionCode = "34067-9",
            string sectionType = "INDICATIONS AND USAGE",
            int documentId = 1,
            Guid? documentGuid = null,
            Guid? setGuid = null)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_SectionNavigation""
                (SectionID, SectionGUID, SectionCode, SectionType, SectionTitle,
                 DocumentID, DocumentGUID, SetGUID, DocumentTitle, VersionNumber,
                 ContentBlockCount)
                VALUES (1, $secGuid, $code, $type, $type,
                        $docId, $docGuid, $setGuid, 'Test Doc', 1, 5)";
            cmd.Parameters.AddWithValue("$secGuid", TestSectionGuid.ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$code", sectionCode);
            cmd.Parameters.AddWithValue("$type", sectionType);
            cmd.Parameters.AddWithValue("$docId", documentId);
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$setGuid", (setGuid ?? TestSetGuid).ToString("D").ToUpper());
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_SectionTypeSummary backing table via raw SQL.
        /// </summary>
        public static void SeedSectionTypeSummaryView(
            SqliteConnection connection,
            string sectionCode = "34067-9",
            string sectionType = "INDICATIONS AND USAGE",
            int sectionCount = 100,
            int documentCount = 50)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_SectionTypeSummary""
                (SectionCode, SectionType, SectionCount, DocumentCount, LabelSetCount)
                VALUES ($code, $type, $secCount, $docCount, 25)";
            cmd.Parameters.AddWithValue("$code", sectionCode);
            cmd.Parameters.AddWithValue("$type", sectionType);
            cmd.Parameters.AddWithValue("$secCount", sectionCount);
            cmd.Parameters.AddWithValue("$docCount", documentCount);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the tmp_SectionContent backing table via raw SQL.
        /// </summary>
        public static void SeedSectionContentView(
            SqliteConnection connection,
            Guid? documentGuid = null,
            Guid? setGuid = null,
            string sectionCode = "34067-9",
            string sectionTitle = "INDICATIONS AND USAGE",
            string contentText = "This drug is indicated for pain relief.",
            int sequenceNumber = 1)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""tmp_SectionContent""
                (DocumentID, SectionID, DocumentGUID, SetGUID, SectionGUID,
                 VersionNumber, DocumentDisplayName, DocumentTitle,
                 SectionCode, SectionDisplayName, SectionTitle,
                 ContentText, SequenceNumber, ContentType, SectionCodeSystem)
                VALUES (1, 1, $docGuid, $setGuid, $secGuid,
                        1, 'HUMAN PRESCRIPTION DRUG LABEL', 'Test Doc',
                        $code, $secTitle, $secTitle,
                        $text, $seq, 'Paragraph', '2.16.840.1.113883.6.1')";
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$setGuid", (setGuid ?? TestSetGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$secGuid", TestSectionGuid.ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$code", sectionCode);
            cmd.Parameters.AddWithValue("$secTitle", sectionTitle);
            cmd.Parameters.AddWithValue("$text", contentText);
            cmd.Parameters.AddWithValue("$seq", sequenceNumber);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_DrugInteractionLookup backing table via raw SQL.
        /// </summary>
        public static void SeedDrugInteractionLookupView(
            SqliteConnection connection,
            string ingredientUNII = "R16CO5Y76E",
            string ingredientName = "ASPIRIN",
            string productName = "ASPIRIN",
            int documentId = 1,
            Guid? documentGuid = null,
            Guid? setGuid = null)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_DrugInteractionLookup""
                (ProductID, ProductName, IngredientSubstanceID, IngredientUNII,
                 IngredientName, DocumentID, DocumentGUID, SetGUID)
                VALUES (1, $prodName, 1, $unii,
                        $ingredName, $docId, $docGuid, $setGuid)";
            cmd.Parameters.AddWithValue("$prodName", productName);
            cmd.Parameters.AddWithValue("$unii", ingredientUNII);
            cmd.Parameters.AddWithValue("$ingredName", ingredientName);
            cmd.Parameters.AddWithValue("$docId", documentId);
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$setGuid", (setGuid ?? TestSetGuid).ToString("D").ToUpper());
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_DEAScheduleLookup backing table via raw SQL.
        /// </summary>
        public static void SeedDEAScheduleLookupView(
            SqliteConnection connection,
            string deaScheduleCode = "CII",
            string deaSchedule = "Schedule II",
            string productName = "OXYCODONE",
            int documentId = 1,
            Guid? documentGuid = null,
            Guid? setGuid = null)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_DEAScheduleLookup""
                (ProductID, ProductName, DEAScheduleCode, DEASchedule,
                 DocumentID, DocumentGUID, SetGUID)
                VALUES (1, $prodName, $code, $schedule,
                        $docId, $docGuid, $setGuid)";
            cmd.Parameters.AddWithValue("$prodName", productName);
            cmd.Parameters.AddWithValue("$code", deaScheduleCode);
            cmd.Parameters.AddWithValue("$schedule", deaSchedule);
            cmd.Parameters.AddWithValue("$docId", documentId);
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$setGuid", (setGuid ?? TestSetGuid).ToString("D").ToUpper());
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_ProductSummary backing table via raw SQL.
        /// </summary>
        public static void SeedProductSummaryView(
            SqliteConnection connection,
            string productName = "ASPIRIN",
            int productId = 1,
            int documentId = 1,
            Guid? documentGuid = null,
            Guid? setGuid = null,
            string? applicationNumber = "NDA014526")
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_ProductSummary""
                (ProductID, ProductName, DosageFormCode, DosageFormName,
                 ApplicationNumber, DocumentID, DocumentGUID, SetGUID, VersionNumber,
                 DocumentTitle, DocumentCode, DocumentType)
                VALUES ($prodId, $prodName, 'C42998', 'TABLET',
                        $appNum, $docId, $docGuid, $setGuid, 1,
                        'Test Doc', '34391-3', 'HUMAN PRESCRIPTION DRUG LABEL')";
            cmd.Parameters.AddWithValue("$prodId", productId);
            cmd.Parameters.AddWithValue("$prodName", productName);
            cmd.Parameters.AddWithValue("$appNum", (object?)applicationNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$docId", documentId);
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$setGuid", (setGuid ?? TestSetGuid).ToString("D").ToUpper());
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_RelatedProducts backing table via raw SQL.
        /// </summary>
        public static void SeedRelatedProductsView(
            SqliteConnection connection,
            int sourceProductId = 1,
            string sourceProductName = "ASPIRIN",
            Guid? sourceDocumentGuid = null,
            int relatedProductId = 2,
            string relatedProductName = "GENERIC ASPIRIN",
            string relationshipType = "SameIngredient")
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_RelatedProducts""
                (SourceProductID, SourceProductName, SourceDocumentGUID,
                 RelatedProductID, RelatedProductName, RelatedDocumentGUID,
                 RelationshipType)
                VALUES ($srcProdId, $srcName, $srcGuid,
                        $relProdId, $relName, $relGuid,
                        $relType)";
            cmd.Parameters.AddWithValue("$srcProdId", sourceProductId);
            cmd.Parameters.AddWithValue("$srcName", sourceProductName);
            cmd.Parameters.AddWithValue("$srcGuid", (sourceDocumentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$relProdId", relatedProductId);
            cmd.Parameters.AddWithValue("$relName", relatedProductName);
            cmd.Parameters.AddWithValue("$relGuid", TestDocumentGuid2.ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$relType", relationshipType);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_APIEndpointGuide backing table via raw SQL.
        /// </summary>
        public static void SeedAPIEndpointGuideView(
            SqliteConnection connection,
            string viewName = "vw_ProductsByApplicationNumber",
            string endpointName = "SearchByApplicationNumber",
            string category = "Navigation",
            string description = "Search products by application number")
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_APIEndpointGuide""
                (ViewName, EndpointName, Description, Category, UsageHint)
                VALUES ($view, $endpoint, $desc, $cat, 'Use application number')";
            cmd.Parameters.AddWithValue("$view", viewName);
            cmd.Parameters.AddWithValue("$endpoint", endpointName);
            cmd.Parameters.AddWithValue("$desc", description);
            cmd.Parameters.AddWithValue("$cat", category);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the tmp_InventorySummary backing table via raw SQL.
        /// </summary>
        public static void SeedInventorySummaryView(
            SqliteConnection connection,
            string category = "Documents",
            string dimension = "Total",
            string dimensionValue = "All Documents",
            int itemCount = 1000,
            int sortOrder = 1)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""tmp_InventorySummary""
                (Category, Dimension, DimensionValue, ItemCount, SortOrder)
                VALUES ($cat, $dim, $dimVal, $count, $sort)";
            cmd.Parameters.AddWithValue("$cat", category);
            cmd.Parameters.AddWithValue("$dim", dimension);
            cmd.Parameters.AddWithValue("$dimVal", dimensionValue);
            cmd.Parameters.AddWithValue("$count", itemCount);
            cmd.Parameters.AddWithValue("$sort", sortOrder);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_ProductLatestLabel backing table via raw SQL.
        /// </summary>
        public static void SeedProductLatestLabelView(
            SqliteConnection connection,
            string productName = "ASPIRIN",
            string activeIngredient = "ASPIRIN",
            string unii = "R16CO5Y76E",
            Guid? documentGuid = null)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_ProductLatestLabel""
                (ProductName, ActiveIngredient, UNII, DocumentGUID)
                VALUES ($prodName, $ingredient, $unii, $docGuid)";
            cmd.Parameters.AddWithValue("$prodName", productName);
            cmd.Parameters.AddWithValue("$ingredient", activeIngredient);
            cmd.Parameters.AddWithValue("$unii", unii);
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_ProductIndications backing table via raw SQL.
        /// </summary>
        public static void SeedProductIndicationsView(
            SqliteConnection connection,
            string productName = "ASPIRIN",
            string substanceName = "ASPIRIN",
            string unii = "R16CO5Y76E",
            Guid? documentGuid = null,
            string contentText = "Indicated for the treatment of pain and inflammation.")
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_ProductIndications""
                (ProductName, SubstanceName, UNII, DocumentGUID, ContentText)
                VALUES ($prodName, $substName, $unii, $docGuid, $text)";
            cmd.Parameters.AddWithValue("$prodName", productName);
            cmd.Parameters.AddWithValue("$substName", substanceName);
            cmd.Parameters.AddWithValue("$unii", unii);
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$text", contentText);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the tmp_LabelSectionMarkdown backing table via raw SQL.
        /// </summary>
        public static void SeedLabelSectionMarkdownView(
            SqliteConnection connection,
            Guid? documentGuid = null,
            Guid? setGuid = null,
            string documentTitle = "Test Document",
            string sectionCode = "34067-9",
            string sectionTitle = "INDICATIONS AND USAGE",
            string fullSectionText = "## INDICATIONS AND USAGE\nThis drug is indicated for pain.",
            int contentBlockCount = 1)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""tmp_LabelSectionMarkdown""
                (DocumentGUID, SetGUID, DocumentTitle, SectionCode,
                 SectionTitle, SectionKey, FullSectionText, ContentBlockCount)
                VALUES ($docGuid, $setGuid, $docTitle, $code,
                        $secTitle, $code, $text, $blockCount)";
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$setGuid", (setGuid ?? TestSetGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$docTitle", documentTitle);
            cmd.Parameters.AddWithValue("$code", sectionCode);
            cmd.Parameters.AddWithValue("$secTitle", sectionTitle);
            cmd.Parameters.AddWithValue("$text", fullSectionText);
            cmd.Parameters.AddWithValue("$blockCount", contentBlockCount);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a row into the vw_OrangeBookPatent backing table via raw SQL.
        /// </summary>
        public static void SeedOrangeBookPatentView(
            SqliteConnection connection,
            Guid? documentGuid = null,
            string applicationNumber = "NDA014526",
            string applicationType = "NDA",
            string ingredient = "ASPIRIN",
            string tradeName = "ASPIRIN",
            string patentNo = "US1234567",
            DateTime? patentExpireDate = null,
            bool drugSubstanceFlag = true,
            bool drugProductFlag = false,
            bool hasPediatricFlag = false,
            bool hasWithdrawnCommercialReasonFlag = false)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO ""vw_OrangeBookPatent""
                (DocumentGUID, ApplicationType, ApplicationNumber, ProductNo,
                 Ingredient, TradeName, Strength, DosageForm, Route,
                 PatentNo, PatentExpireDate, DrugSubstanceFlag, DrugProductFlag,
                 HasPediatricFlag, HasWithdrawnCommercialReasonFlag, DelistFlag, HasLevothyroxineFlag)
                VALUES ($docGuid, $appType, $appNum, '001',
                        $ingredient, $tradeName, '325mg', 'TABLET', 'ORAL',
                        $patentNo, $expDate, $drugSubst, $drugProd,
                        $pediatric, $withdrawn, 0, 0)";
            cmd.Parameters.AddWithValue("$docGuid", (documentGuid ?? TestDocumentGuid).ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$appType", applicationType);
            cmd.Parameters.AddWithValue("$appNum", applicationNumber);
            cmd.Parameters.AddWithValue("$ingredient", ingredient);
            cmd.Parameters.AddWithValue("$tradeName", tradeName);
            cmd.Parameters.AddWithValue("$patentNo", patentNo);
            cmd.Parameters.AddWithValue("$expDate", (patentExpireDate ?? new DateTime(2030, 6, 15)).ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$drugSubst", drugSubstanceFlag ? 1 : 0);
            cmd.Parameters.AddWithValue("$drugProd", drugProductFlag ? 1 : 0);
            cmd.Parameters.AddWithValue("$pediatric", hasPediatricFlag ? 1 : 0);
            cmd.Parameters.AddWithValue("$withdrawn", hasWithdrawnCommercialReasonFlag ? 1 : 0);
            cmd.ExecuteNonQuery();

            #endregion
        }

        #endregion LabelView Raw SQL Seed Methods
    }
}
