using MedRecProImportClass.Data;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Text.RegularExpressions;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="BioequivalentLabelDedupService"/>. Exercises the
    /// grouping + tier-selection + ordering contract using an in-memory SQLite
    /// database seeded with synthetic OrangeBookProduct rows and a hand-built
    /// <c>vw_ProductsByLabeler</c> backing table (the production view is keyless
    /// and <c>ToView</c>-registered, so <c>GenerateCreateScript</c> does not emit
    /// DDL for it — we create the backing table manually).
    /// </summary>
    /// <seealso cref="BioequivalentLabelDedupService"/>
    /// <seealso cref="IBioequivalentLabelDedupService"/>
    [TestClass]
    public class BioequivalentLabelDedupServiceTests
    {
        #region Helpers — infrastructure

        /**************************************************************/
        /// <summary>
        /// Shared-cache named SQLite DB with a sentinel connection that outlives
        /// the service's finally { connection.CloseAsync() } block. Tests close
        /// their connection in the finally to mimic production lifetime.
        /// </summary>
        private static (SqliteConnection sentinel, SqliteConnection connection) createSharedMemoryDb()
        {
            #region implementation

            var dbName = $"file:dedup_test_{Guid.NewGuid():N}?mode=memory&cache=shared";
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
        /// Builds an <see cref="ApplicationDbContext"/> against the given SQLite
        /// connection, applies patched DDL for all EF-managed entities, and manually
        /// adds the <c>vw_ProductsByLabeler</c> backing table because that entity
        /// is keyless-read-only and excluded from generated DDL.
        /// </summary>
        private static ApplicationDbContext createTestContext(SqliteConnection connection)
        {
            #region implementation

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ApplicationDbContext(options);

            // Patch SQL-Server-specific DDL for SQLite compatibility.
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
                    // Skip SQL-Server-only constructs (filtered indexes, etc.).
                }
            }

            // vw_ProductsByLabeler is registered via ToView — no DDL is generated.
            // Create a backing table with matching column names so the EF query materializes.
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS vw_ProductsByLabeler (
                        LabelerOrgID INTEGER,
                        LabelerName TEXT,
                        OrganizationIdentifierID INTEGER,
                        OrgIdentifierValue TEXT,
                        OrgIdentifierType TEXT,
                        ProductID INTEGER,
                        ProductName TEXT,
                        DosageFormCode TEXT,
                        DosageFormName TEXT,
                        GenericName TEXT,
                        ApplicationNumber TEXT,
                        MarketingCategory TEXT,
                        DocumentID INTEGER,
                        DocumentGUID TEXT,
                        SetGUID TEXT,
                        VersionNumber INTEGER,
                        DocumentTitle TEXT,
                        LabelEffectiveDate TEXT
                    );";
                cmd.ExecuteNonQuery();
            }

            return context;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Constructs the dedup service under test with a no-op logger.
        /// </summary>
        private static BioequivalentLabelDedupService createService(ApplicationDbContext context)
        {
            #region implementation

            var logger = new Mock<ILogger<BioequivalentLabelDedupService>>();
            return new BioequivalentLabelDedupService(context, logger.Object);

            #endregion
        }

        #endregion

        #region Helpers — seed

        /**************************************************************/
        /// <summary>
        /// Inserts one row into the vw_ProductsByLabeler backing table. GUID formatted
        /// uppercase (matches EF Core 8 SQLite query parameter format).
        /// </summary>
        private static void seedLabelerRow(
            SqliteConnection connection,
            Guid documentGuid,
            string? applicationNumber,
            DateTime? labelEffectiveDate,
            int? versionNumber)
        {
            #region implementation

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO vw_ProductsByLabeler (DocumentGUID, ApplicationNumber, LabelEffectiveDate, VersionNumber)
                VALUES ($guid, $app, $date, $ver);";
            cmd.Parameters.AddWithValue("$guid", documentGuid.ToString("D").ToUpper());
            cmd.Parameters.AddWithValue("$app", (object?)applicationNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$date", (object?)labelEffectiveDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ver", (object?)versionNumber ?? DBNull.Value);
            cmd.ExecuteNonQuery();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds one OrangeBookProduct row via the EF context. Uses the default
        /// ProductNo "001" since the dedup service groups on Ingredient/DosageForm/Route
        /// and ignores ProductNo.
        /// </summary>
        private static void seedOrangeBookProduct(
            ApplicationDbContext context,
            string applType, string applNo,
            string ingredient, string dosageForm, string route,
            bool isRld = false,
            DateTime? approvalDate = null)
        {
            #region implementation

            context.Set<OrangeBook.Product>().Add(new OrangeBook.Product
            {
                ApplType = applType,
                ApplNo = applNo,
                ProductNo = "001",
                Ingredient = ingredient,
                DosageForm = dosageForm,
                Route = route,
                IsRLD = isRld,
                ApprovalDate = approvalDate
            });
            context.SaveChanges();

            #endregion
        }

        #endregion

        #region Tests — NDA/ANDA preference

        /**************************************************************/
        /// <summary>
        /// When a group has both NDA and ANDA labels, the canonical slot must be the NDA
        /// regardless of which label has the more recent LabelEffectiveDate — NDA
        /// preference trumps recency in v1 per the approved plan.
        /// </summary>
        [TestMethod]
        public async Task DeduplicateAsync_NdaAndAndasInSameGroup_KeepsOnlyNda()
        {
            var (sentinel, connection) = createSharedMemoryDb();
            try
            {
                using var context = createTestContext(connection);

                var ndaGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
                var andaGuid1 = Guid.Parse("00000000-0000-0000-0000-000000000002");
                var andaGuid2 = Guid.Parse("00000000-0000-0000-0000-000000000003");
                var andaGuid3 = Guid.Parse("00000000-0000-0000-0000-000000000004");

                // NDA label — older LabelEffectiveDate than some ANDAs, but should still win.
                seedLabelerRow(connection, ndaGuid, "NDA020610", new DateTime(2025, 1, 1), 1);
                seedLabelerRow(connection, andaGuid1, "ANDA090083", new DateTime(2025, 8, 6), 24);
                seedLabelerRow(connection, andaGuid2, "ANDA078243", new DateTime(2025, 7, 26), 19);
                seedLabelerRow(connection, andaGuid3, "ANDA202230", new DateTime(2025, 12, 29), 8);

                seedOrangeBookProduct(context, "N", "020610", "LOSARTAN POTASSIUM", "TABLET, FILM COATED", "ORAL", isRld: true);
                seedOrangeBookProduct(context, "A", "090083", "LOSARTAN POTASSIUM", "TABLET, FILM COATED", "ORAL");
                seedOrangeBookProduct(context, "A", "078243", "LOSARTAN POTASSIUM", "TABLET, FILM COATED", "ORAL");
                seedOrangeBookProduct(context, "A", "202230", "LOSARTAN POTASSIUM", "TABLET, FILM COATED", "ORAL");

                var service = createService(context);

                var result = await service.DeduplicateAsync(
                    new[] { ndaGuid, andaGuid1, andaGuid2, andaGuid3 });

                Assert.AreEqual(1, result.KeptDocumentGuids.Count);
                Assert.AreEqual(ndaGuid, result.KeptDocumentGuids[0]);
                Assert.AreEqual(1, result.GroupCount);
                Assert.AreEqual(1, result.NdaGroupCount);
                Assert.AreEqual(0, result.AndaGroupCount);
                Assert.AreEqual(3, result.DroppedDocuments.Count);
                foreach (var dropped in result.DroppedDocuments)
                {
                    StringAssert.StartsWith(dropped.Reason, "bioequivalent_duplicate");
                }
            }
            finally
            {
                connection?.Dispose();
                sentinel?.Dispose();
            }
        }

        /**************************************************************/
        /// <summary>
        /// No NDA exists — mirrors the Losartan example in the issue. Three ANDAs
        /// compete and the one with the most recent LabelEffectiveDate wins.
        /// </summary>
        [TestMethod]
        public async Task DeduplicateAsync_AndasOnly_KeepsMostRecentLabelEffectiveDate()
        {
            var (sentinel, connection) = createSharedMemoryDb();
            try
            {
                using var context = createTestContext(connection);

                var old = Guid.Parse("00000000-0000-0000-0000-000000000010");
                var mid = Guid.Parse("00000000-0000-0000-0000-000000000011");
                var latest = Guid.Parse("00000000-0000-0000-0000-000000000012");

                seedLabelerRow(connection, old, "ANDA090083", new DateTime(2025, 7, 1), 10);
                seedLabelerRow(connection, mid, "ANDA078243", new DateTime(2025, 9, 15), 15);
                seedLabelerRow(connection, latest, "ANDA202230", new DateTime(2025, 12, 29), 8);

                seedOrangeBookProduct(context, "A", "090083", "LOSARTAN POTASSIUM", "TABLET, FILM COATED", "ORAL");
                seedOrangeBookProduct(context, "A", "078243", "LOSARTAN POTASSIUM", "TABLET, FILM COATED", "ORAL");
                seedOrangeBookProduct(context, "A", "202230", "LOSARTAN POTASSIUM", "TABLET, FILM COATED", "ORAL");

                var service = createService(context);

                var result = await service.DeduplicateAsync(new[] { old, mid, latest });

                Assert.AreEqual(1, result.KeptDocumentGuids.Count);
                Assert.AreEqual(latest, result.KeptDocumentGuids[0]);
                Assert.AreEqual(1, result.AndaGroupCount);
                Assert.AreEqual(0, result.NdaGroupCount);
            }
            finally
            {
                connection?.Dispose();
                sentinel?.Dispose();
            }
        }

        /**************************************************************/
        /// <summary>
        /// Repackager scenario: one ApplicationNumber, N DocumentGUIDs (original
        /// manufacturer + repackagers). All share the same bioequivalent group, so the
        /// filter must collapse them to one canonical label.
        /// </summary>
        [TestMethod]
        public async Task DeduplicateAsync_SameApplicationNumberMultipleLabels_KeepsLatestLabel()
        {
            var (sentinel, connection) = createSharedMemoryDb();
            try
            {
                using var context = createTestContext(connection);

                var original = Guid.Parse("00000000-0000-0000-0000-000000000020");
                var repack1 = Guid.Parse("00000000-0000-0000-0000-000000000021");
                var repack2 = Guid.Parse("00000000-0000-0000-0000-000000000022");
                var repack3 = Guid.Parse("00000000-0000-0000-0000-000000000023");
                var repack4 = Guid.Parse("00000000-0000-0000-0000-000000000024");

                seedLabelerRow(connection, original, "ANDA090083", new DateTime(2025, 8, 6), 24);
                seedLabelerRow(connection, repack1, "ANDA090083", new DateTime(2025, 9, 18), 104);
                seedLabelerRow(connection, repack2, "ANDA090083", new DateTime(2025, 9, 25), 27);
                seedLabelerRow(connection, repack3, "ANDA090083", new DateTime(2025, 10, 3), 18);
                seedLabelerRow(connection, repack4, "ANDA090083", new DateTime(2025, 12, 15), 19);

                seedOrangeBookProduct(context, "A", "090083", "LOSARTAN POTASSIUM", "TABLET, FILM COATED", "ORAL");

                var service = createService(context);

                var result = await service.DeduplicateAsync(
                    new[] { original, repack1, repack2, repack3, repack4 });

                Assert.AreEqual(1, result.KeptDocumentGuids.Count);
                Assert.AreEqual(repack4, result.KeptDocumentGuids[0]);
                Assert.AreEqual(4, result.DroppedDocuments.Count);
            }
            finally
            {
                connection?.Dispose();
                sentinel?.Dispose();
            }
        }

        /**************************************************************/
        /// <summary>
        /// Deterministic tie-breakers: equal LabelEffectiveDate → higher VersionNumber wins;
        /// equal VersionNumber → lower DocumentGUID wins.
        /// </summary>
        [TestMethod]
        public async Task DeduplicateAsync_TieOnLabelDate_BreaksOnVersionThenGuid()
        {
            var (sentinel, connection) = createSharedMemoryDb();
            try
            {
                using var context = createTestContext(connection);

                // Same date — tie breaks on version.
                var lowVersion = Guid.Parse("00000000-0000-0000-0000-000000000030");
                var highVersion = Guid.Parse("00000000-0000-0000-0000-000000000031");
                // Same date AND version — tie breaks on GUID.
                var lowGuid = Guid.Parse("00000000-0000-0000-0000-000000000040");
                var highGuid = Guid.Parse("00000000-0000-0000-0000-000000000041");

                var sharedDate = new DateTime(2025, 5, 15);

                // Group 1: tied-date tied-version → GUID wins.
                seedLabelerRow(connection, highGuid, "ANDA100000", sharedDate, 5);
                seedLabelerRow(connection, lowGuid, "ANDA100000", sharedDate, 5);
                seedOrangeBookProduct(context, "A", "100000", "DRUG A", "TABLET", "ORAL");

                // Group 2: tied-date different-version → higher version wins.
                seedLabelerRow(connection, lowVersion, "ANDA200000", sharedDate, 3);
                seedLabelerRow(connection, highVersion, "ANDA200000", sharedDate, 9);
                seedOrangeBookProduct(context, "A", "200000", "DRUG B", "TABLET", "ORAL");

                var service = createService(context);

                var result = await service.DeduplicateAsync(
                    new[] { highGuid, lowGuid, lowVersion, highVersion });

                CollectionAssert.Contains(result.KeptDocumentGuids.ToList(), lowGuid);
                CollectionAssert.DoesNotContain(result.KeptDocumentGuids.ToList(), highGuid);
                CollectionAssert.Contains(result.KeptDocumentGuids.ToList(), highVersion);
                CollectionAssert.DoesNotContain(result.KeptDocumentGuids.ToList(), lowVersion);
            }
            finally
            {
                connection?.Dispose();
                sentinel?.Dispose();
            }
        }

        #endregion

        #region Tests — unclassifiable handling

        /**************************************************************/
        /// <summary>
        /// A DocumentGUID with no entry in vw_ProductsByLabeler has no resolvable
        /// ApplicationNumber and must be dropped when DropUnclassifiable is true (default).
        /// </summary>
        [TestMethod]
        public async Task DeduplicateAsync_NoMarketingCategory_DroppedWhenDropUnclassifiableTrue()
        {
            var (sentinel, connection) = createSharedMemoryDb();
            try
            {
                using var context = createTestContext(connection);

                var orphan = Guid.Parse("00000000-0000-0000-0000-000000000050");
                var service = createService(context);

                var result = await service.DeduplicateAsync(new[] { orphan });

                Assert.AreEqual(0, result.KeptDocumentGuids.Count);
                Assert.AreEqual(1, result.DroppedDocuments.Count);
                Assert.AreEqual("unclassifiable:no_application_number", result.DroppedDocuments[0].Reason);
                Assert.AreEqual(1, result.UnclassifiableCount);
            }
            finally
            {
                connection?.Dispose();
                sentinel?.Dispose();
            }
        }

        /**************************************************************/
        /// <summary>
        /// ApplicationNumber present but no matching Orange Book row → dropped with the
        /// <c>no_orange_book_match</c> reason (distinct from <c>no_application_number</c>).
        /// </summary>
        [TestMethod]
        public async Task DeduplicateAsync_NoOrangeBookMatch_DroppedWhenDropUnclassifiableTrue()
        {
            var (sentinel, connection) = createSharedMemoryDb();
            try
            {
                using var context = createTestContext(connection);

                var doc = Guid.Parse("00000000-0000-0000-0000-000000000060");
                seedLabelerRow(connection, doc, "ANDA999999", new DateTime(2025, 1, 1), 1);
                // No matching OrangeBookProduct row.

                var service = createService(context);

                var result = await service.DeduplicateAsync(new[] { doc });

                Assert.AreEqual(0, result.KeptDocumentGuids.Count);
                Assert.AreEqual(1, result.DroppedDocuments.Count);
                Assert.AreEqual("unclassifiable:no_orange_book_match", result.DroppedDocuments[0].Reason);
            }
            finally
            {
                connection?.Dispose();
                sentinel?.Dispose();
            }
        }

        /**************************************************************/
        /// <summary>
        /// ApplicationNumber with an unrecognized prefix (e.g. BLA, OTC monograph) is
        /// dropped with the <c>unrecognized_prefix</c> reason.
        /// </summary>
        [TestMethod]
        public async Task DeduplicateAsync_UnknownPrefix_DroppedAsUnrecognizedPrefix()
        {
            var (sentinel, connection) = createSharedMemoryDb();
            try
            {
                using var context = createTestContext(connection);

                var doc = Guid.Parse("00000000-0000-0000-0000-000000000070");
                seedLabelerRow(connection, doc, "BLA125557", new DateTime(2025, 1, 1), 1);

                var service = createService(context);

                var result = await service.DeduplicateAsync(new[] { doc });

                Assert.AreEqual(0, result.KeptDocumentGuids.Count);
                Assert.AreEqual(1, result.DroppedDocuments.Count);
                Assert.AreEqual("unclassifiable:unrecognized_prefix", result.DroppedDocuments[0].Reason);
            }
            finally
            {
                connection?.Dispose();
                sentinel?.Dispose();
            }
        }

        /**************************************************************/
        /// <summary>
        /// With DropUnclassifiable = false, unclassifiable docs are retained with no
        /// dedup applied to them.
        /// </summary>
        [TestMethod]
        public async Task DeduplicateAsync_KeepUnclassifiable_PreservesAll()
        {
            var (sentinel, connection) = createSharedMemoryDb();
            try
            {
                using var context = createTestContext(connection);

                var orphan = Guid.Parse("00000000-0000-0000-0000-000000000080");
                var unknown = Guid.Parse("00000000-0000-0000-0000-000000000081");
                seedLabelerRow(connection, unknown, "BLA125557", new DateTime(2025, 1, 1), 1);

                var service = createService(context);

                var result = await service.DeduplicateAsync(
                    new[] { orphan, unknown },
                    new BioequivalentDedupOptions { DropUnclassifiable = false });

                Assert.AreEqual(2, result.KeptDocumentGuids.Count);
                Assert.AreEqual(0, result.DroppedDocuments.Count);
            }
            finally
            {
                connection?.Dispose();
                sentinel?.Dispose();
            }
        }

        #endregion

        #region Tests — ordering, edge cases, normalization

        /**************************************************************/
        /// <summary>
        /// The UNII-ordered input list must emerge with its original relative order
        /// preserved among the kept GUIDs (critical for ML training-data locality).
        /// </summary>
        [TestMethod]
        public async Task DeduplicateAsync_PreservesInputOrder()
        {
            var (sentinel, connection) = createSharedMemoryDb();
            try
            {
                using var context = createTestContext(connection);

                // Three distinct bioequivalent groups — each collapses to itself (single doc per group).
                var first = Guid.Parse("00000000-0000-0000-0000-0000000000A1");
                var second = Guid.Parse("00000000-0000-0000-0000-0000000000A2");
                var third = Guid.Parse("00000000-0000-0000-0000-0000000000A3");

                seedLabelerRow(connection, first, "ANDA100001", new DateTime(2025, 1, 1), 1);
                seedLabelerRow(connection, second, "ANDA100002", new DateTime(2025, 1, 2), 1);
                seedLabelerRow(connection, third, "ANDA100003", new DateTime(2025, 1, 3), 1);
                seedOrangeBookProduct(context, "A", "100001", "DRUG A", "TABLET", "ORAL");
                seedOrangeBookProduct(context, "A", "100002", "DRUG B", "TABLET", "ORAL");
                seedOrangeBookProduct(context, "A", "100003", "DRUG C", "TABLET", "ORAL");

                var service = createService(context);

                var result = await service.DeduplicateAsync(new[] { first, second, third });

                Assert.AreEqual(3, result.KeptDocumentGuids.Count);
                Assert.AreEqual(first, result.KeptDocumentGuids[0]);
                Assert.AreEqual(second, result.KeptDocumentGuids[1]);
                Assert.AreEqual(third, result.KeptDocumentGuids[2]);
            }
            finally
            {
                connection?.Dispose();
                sentinel?.Dispose();
            }
        }

        /**************************************************************/
        /// <summary>
        /// Empty input returns an empty, fully-populated result — callers should not
        /// need to null-check any of the collections.
        /// </summary>
        [TestMethod]
        public async Task DeduplicateAsync_EmptyInput_ReturnsEmptyResult()
        {
            var (sentinel, connection) = createSharedMemoryDb();
            try
            {
                using var context = createTestContext(connection);
                var service = createService(context);

                var result = await service.DeduplicateAsync(Array.Empty<Guid>());

                Assert.IsNotNull(result.KeptDocumentGuids);
                Assert.IsNotNull(result.DroppedDocuments);
                Assert.AreEqual(0, result.KeptDocumentGuids.Count);
                Assert.AreEqual(0, result.DroppedDocuments.Count);
                Assert.AreEqual(0, result.GroupCount);
            }
            finally
            {
                connection?.Dispose();
                sentinel?.Dispose();
            }
        }

        /**************************************************************/
        /// <summary>
        /// Combination products (e.g. Losartan + HCTZ) have a different Ingredient
        /// string and must form their own bioequivalent group, independent of
        /// the mono-ingredient Losartan group.
        /// </summary>
        [TestMethod]
        public async Task DeduplicateAsync_CombinationProductVsMonoProduct_SeparateGroups()
        {
            var (sentinel, connection) = createSharedMemoryDb();
            try
            {
                using var context = createTestContext(connection);

                var mono = Guid.Parse("00000000-0000-0000-0000-0000000000B1");
                var combo = Guid.Parse("00000000-0000-0000-0000-0000000000B2");

                seedLabelerRow(connection, mono, "ANDA100001", new DateTime(2025, 1, 1), 1);
                seedLabelerRow(connection, combo, "ANDA100002", new DateTime(2025, 1, 2), 1);
                seedOrangeBookProduct(context, "A", "100001", "LOSARTAN POTASSIUM", "TABLET, FILM COATED", "ORAL");
                seedOrangeBookProduct(context, "A", "100002", "LOSARTAN POTASSIUM; HYDROCHLOROTHIAZIDE", "TABLET, FILM COATED", "ORAL");

                var service = createService(context);

                var result = await service.DeduplicateAsync(new[] { mono, combo });

                Assert.AreEqual(2, result.KeptDocumentGuids.Count);
                Assert.AreEqual(2, result.GroupCount);
            }
            finally
            {
                connection?.Dispose();
                sentinel?.Dispose();
            }
        }

        /**************************************************************/
        /// <summary>
        /// Same ingredient + route but different dosage form (TABLET vs CAPSULE) =
        /// separate bioequivalent groups, so both labels are kept.
        /// </summary>
        [TestMethod]
        public async Task DeduplicateAsync_DosageFormDifference_SeparateGroups()
        {
            var (sentinel, connection) = createSharedMemoryDb();
            try
            {
                using var context = createTestContext(connection);

                var tablet = Guid.Parse("00000000-0000-0000-0000-0000000000C1");
                var capsule = Guid.Parse("00000000-0000-0000-0000-0000000000C2");

                seedLabelerRow(connection, tablet, "ANDA100001", new DateTime(2025, 1, 1), 1);
                seedLabelerRow(connection, capsule, "ANDA100002", new DateTime(2025, 1, 2), 1);
                seedOrangeBookProduct(context, "A", "100001", "DRUG A", "TABLET", "ORAL");
                seedOrangeBookProduct(context, "A", "100002", "DRUG A", "CAPSULE", "ORAL");

                var service = createService(context);

                var result = await service.DeduplicateAsync(new[] { tablet, capsule });

                Assert.AreEqual(2, result.KeptDocumentGuids.Count);
                Assert.AreEqual(2, result.GroupCount);
            }
            finally
            {
                connection?.Dispose();
                sentinel?.Dispose();
            }
        }

        /**************************************************************/
        /// <summary>
        /// Trailing whitespace on the SPL ApplicationNumber value (observed in the
        /// wild — see user's data dump) must still match the Orange Book row.
        /// </summary>
        [TestMethod]
        public async Task DeduplicateAsync_ApplicationNumberWhitespace_HandledCorrectly()
        {
            var (sentinel, connection) = createSharedMemoryDb();
            try
            {
                using var context = createTestContext(connection);

                var doc = Guid.Parse("00000000-0000-0000-0000-0000000000D1");
                seedLabelerRow(connection, doc, "ANDA202230 ", new DateTime(2025, 7, 28), 8);
                seedOrangeBookProduct(context, "A", "202230", "LOSARTAN POTASSIUM", "TABLET, FILM COATED", "ORAL");

                var service = createService(context);

                var result = await service.DeduplicateAsync(new[] { doc });

                Assert.AreEqual(1, result.KeptDocumentGuids.Count);
                Assert.AreEqual(doc, result.KeptDocumentGuids[0]);
            }
            finally
            {
                connection?.Dispose();
                sentinel?.Dispose();
            }
        }

        /**************************************************************/
        /// <summary>
        /// Whitespace variations inside Ingredient / DosageForm strings must collapse
        /// to a single group key. "TABLET,  FILM COATED" (double space) and
        /// "TABLET, FILM COATED" both normalize to the same key.
        /// </summary>
        [TestMethod]
        public async Task DeduplicateAsync_WhitespaceNormalization_CollapsesGroups()
        {
            var (sentinel, connection) = createSharedMemoryDb();
            try
            {
                using var context = createTestContext(connection);

                var a = Guid.Parse("00000000-0000-0000-0000-0000000000E1");
                var b = Guid.Parse("00000000-0000-0000-0000-0000000000E2");

                seedLabelerRow(connection, a, "ANDA100001", new DateTime(2025, 1, 1), 1);
                seedLabelerRow(connection, b, "ANDA100002", new DateTime(2025, 2, 1), 1);
                seedOrangeBookProduct(context, "A", "100001", "DRUG A", "TABLET, FILM COATED", "ORAL");
                seedOrangeBookProduct(context, "A", "100002", "DRUG A", "TABLET,  FILM COATED", "ORAL");  // double space

                var service = createService(context);

                var result = await service.DeduplicateAsync(new[] { a, b });

                Assert.AreEqual(1, result.KeptDocumentGuids.Count);
                Assert.AreEqual(b, result.KeptDocumentGuids[0]);
            }
            finally
            {
                connection?.Dispose();
                sentinel?.Dispose();
            }
        }

        /**************************************************************/
        /// <summary>
        /// Sanity check: the result object populates every metric correctly.
        /// </summary>
        [TestMethod]
        public async Task DeduplicateAsync_ResultMetricsPopulated()
        {
            var (sentinel, connection) = createSharedMemoryDb();
            try
            {
                using var context = createTestContext(connection);

                // One NDA-led group, one ANDA-led group, one unclassifiable.
                var ndaDoc = Guid.Parse("00000000-0000-0000-0000-0000000000F1");
                var andaDoc = Guid.Parse("00000000-0000-0000-0000-0000000000F2");
                var orphan = Guid.Parse("00000000-0000-0000-0000-0000000000F3");

                seedLabelerRow(connection, ndaDoc, "NDA020610", new DateTime(2025, 1, 1), 1);
                seedLabelerRow(connection, andaDoc, "ANDA100002", new DateTime(2025, 1, 2), 1);
                seedOrangeBookProduct(context, "N", "020610", "DRUG A", "TABLET", "ORAL");
                seedOrangeBookProduct(context, "A", "100002", "DRUG B", "TABLET", "ORAL");

                var service = createService(context);

                var result = await service.DeduplicateAsync(new[] { ndaDoc, andaDoc, orphan });

                Assert.AreEqual(2, result.KeptDocumentGuids.Count);
                Assert.AreEqual(2, result.GroupCount);
                Assert.AreEqual(1, result.NdaGroupCount);
                Assert.AreEqual(1, result.AndaGroupCount);
                Assert.AreEqual(1, result.UnclassifiableCount);
                Assert.AreEqual(1, result.DroppedDocuments.Count);
            }
            finally
            {
                connection?.Dispose();
                sentinel?.Dispose();
            }
        }

        /**************************************************************/
        /// <summary>
        /// Null input is treated as empty — no exceptions.
        /// </summary>
        [TestMethod]
        public async Task DeduplicateAsync_NullInput_ReturnsEmptyResult()
        {
            var (sentinel, connection) = createSharedMemoryDb();
            try
            {
                using var context = createTestContext(connection);
                var service = createService(context);

                var result = await service.DeduplicateAsync(null!);

                Assert.AreEqual(0, result.KeptDocumentGuids.Count);
                Assert.AreEqual(0, result.DroppedDocuments.Count);
            }
            finally
            {
                connection?.Dispose();
                sentinel?.Dispose();
            }
        }

        #endregion
    }
}
