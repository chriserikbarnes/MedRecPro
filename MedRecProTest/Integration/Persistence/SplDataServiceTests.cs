using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecProTest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace MedRecProTest.Integration.Persistence
{
    /**************************************************************/
    /// <summary>
    /// Exercises <see cref="SplDataService"/> create, archive, and list
    /// operations over a real <see cref="Repository{T}"/> backed by SQLite.
    /// </summary>
    /// <remarks>
    /// The service is exercised end-to-end (no repository mocks) so the
    /// encrypted-ID round trips, SHA-256 content hashing, and archive
    /// filtering reflect real production behavior. Uses the shared fixed PK
    /// secret from <see cref="DtoLabelAccessTestHelper"/>.
    /// </remarks>
    /// <seealso cref="SplDataService"/>
    /// <seealso cref="Repository{T}"/>
    /// <seealso cref="SplData"/>
    [TestClass]
    [TestCategory("Integration")]
    public class SplDataServiceTests
    {
        #region implementation

        /// <summary>
        /// Fixed PK secret shared with <see cref="DtoLabelAccessTestHelper"/>.
        /// </summary>
        private const string TestPkSecret = DtoLabelAccessTestHelper.TestPkSecret;

        /**************************************************************/
        /// <summary>
        /// Clears the process-wide managed cache and rebinds Util statics
        /// before each test.
        /// </summary>
        /// <seealso cref="DtoLabelAccessTestHelper.ClearCache"/>
        [TestInitialize]
        public void TestInitialize()
        {
            #region implementation
            DtoLabelAccessTestHelper.ClearCache();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies CreateSplDataAsync rejects null, empty, and whitespace XML.
        /// </summary>
        /// <seealso cref="SplDataService.CreateSplDataAsync"/>
        [TestMethod]
        public async Task CreateSplDataAsync_EmptyXml_ThrowsArgumentException()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var service = createService(context);

            // Act + Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => service.CreateSplDataAsync(string.Empty, Guid.NewGuid()));
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => service.CreateSplDataAsync("   ", Guid.NewGuid()));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies CreateSplDataAsync persists the row with a SHA-256 content
        /// hash and returns an encrypted ID that round-trips to the new key.
        /// </summary>
        /// <seealso cref="SplDataService.CreateSplDataAsync"/>
        [TestMethod]
        public async Task CreateSplDataAsync_ValidXml_ReturnsEncryptedIdAndPersistsHash()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var service = createService(context);
            var splGuid = Guid.NewGuid();
            const string xmlContent = "<document>\r\n  spl fixture\r\n</document>";

            // Act
            var encryptedId = await service.CreateSplDataAsync(xmlContent, splGuid, userId: 7);

            // Assert - encrypted ID decrypts to the persisted key.
            Assert.IsFalse(string.IsNullOrWhiteSpace(encryptedId));
            var saved = context.SplData.Single();
            Assert.AreEqual(saved.SplDataID, long.Parse(encryptedId.Decrypt(TestPkSecret)!));

            // Row carries the GUID, user, and normalized-content SHA-256 hash.
            Assert.AreEqual(splGuid, saved.SplDataGUID);
            Assert.AreEqual(7L, saved.AspNetUsersID);
            Assert.AreEqual(computeExpectedHash(xmlContent), saved.SplXMLHash);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies ArchiveSplDataAsync rejects null, empty, and whitespace
        /// encrypted IDs.
        /// </summary>
        /// <seealso cref="SplDataService.ArchiveSplDataAsync"/>
        [TestMethod]
        public async Task ArchiveSplDataAsync_MissingEncryptedId_ThrowsArgumentException()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var service = createService(context);

            // Act + Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => service.ArchiveSplDataAsync(string.Empty));
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => service.ArchiveSplDataAsync("   "));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies ArchiveSplDataAsync returns false when the ID does not
        /// resolve to a record (undecryptable ciphertext or nonexistent key).
        /// </summary>
        /// <seealso cref="SplDataService.ArchiveSplDataAsync"/>
        [TestMethod]
        public async Task ArchiveSplDataAsync_NotFound_ReturnsFalse()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var service = createService(context);
            var missingId = StringCipher.Encrypt("999", TestPkSecret, StringCipher.EncryptionStrength.Fast);

            // Act
            var fromGarbage = await service.ArchiveSplDataAsync("not-cipher-text");
            var fromMissing = await service.ArchiveSplDataAsync(missingId);

            // Assert
            Assert.IsFalse(fromGarbage, "Undecryptable IDs resolve to no record and must return false.");
            Assert.IsFalse(fromMissing, "A valid ciphertext for a nonexistent key must return false.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies ArchiveSplDataAsync soft-deletes an existing record and
        /// reports success.
        /// </summary>
        /// <seealso cref="SplDataService.ArchiveSplDataAsync"/>
        [TestMethod]
        public async Task ArchiveSplDataAsync_ExistingRecord_SetsArchiveAndReturnsTrue()
        {
            #region implementation
            // Arrange - create through the service to get a real encrypted ID.
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var service = createService(context);
            var encryptedId = await service.CreateSplDataAsync("<document>archive me</document>", Guid.NewGuid());

            // Act
            var archived = await service.ArchiveSplDataAsync(encryptedId);

            // Assert
            Assert.IsTrue(archived);
            Assert.IsTrue(context.SplData.Single().Archive ?? false, "The archive flag must persist.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetSplDataListAsync filters archived rows by default and
        /// assigns a decryptable encrypted ID to every returned record.
        /// </summary>
        /// <seealso cref="SplDataService.GetSplDataListAsync"/>
        [TestMethod]
        public async Task GetSplDataListAsync_ExcludeArchived_FiltersRowsAndSetsEncryptedIds()
        {
            #region implementation
            // Arrange - two active rows and one archived row.
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            seedRows(context, activeCount: 2, archivedCount: 1);
            var service = createService(context);

            // Act
            var records = (await service.GetSplDataListAsync()).ToList();

            // Assert - archived row filtered out.
            Assert.AreEqual(2, records.Count);
            Assert.IsTrue(records.All(r => r.Archive != true));

            // Every record carries an encrypted ID that round-trips to its key.
            foreach (var record in records)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(record.EncryptedSplDataId));
                Assert.AreEqual(record.SplDataID, long.Parse(record.EncryptedSplDataId!.Decrypt(TestPkSecret)!));
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetSplDataListAsync includes archived rows when requested.
        /// </summary>
        /// <seealso cref="SplDataService.GetSplDataListAsync"/>
        [TestMethod]
        public async Task GetSplDataListAsync_IncludeArchived_ReturnsArchivedRows()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            seedRows(context, activeCount: 2, archivedCount: 1);
            var service = createService(context);

            // Act
            var records = (await service.GetSplDataListAsync(includeArchived: true)).ToList();

            // Assert
            Assert.AreEqual(3, records.Count);
            Assert.AreEqual(1, records.Count(r => r.Archive == true));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates the service under test over a real SQLite-backed repository.
        /// </summary>
        /// <param name="context">SQLite-backed application context.</param>
        /// <returns>A functional <see cref="SplDataService"/>.</returns>
        /// <seealso cref="Repository{T}"/>
        private static SplDataService createService(MedRecPro.Data.ApplicationDbContext context)
        {
            #region implementation
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:DB:PKSecret"] = TestPkSecret,
                    ["FeatureFlags:UseBatchDocumentLoading"] = "false"
                })
                .Build();

            var repository = new Repository<SplData>(
                context, new StringCipher(), new Mock<ILogger<SplData>>().Object, configuration);

            return new SplDataService(repository, context, NullLogger<SplDataService>.Instance, configuration);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds active and archived SplData rows directly through the context.
        /// </summary>
        /// <param name="context">Context to insert into.</param>
        /// <param name="activeCount">Number of active rows.</param>
        /// <param name="archivedCount">Number of archived rows.</param>
        private static void seedRows(MedRecPro.Data.ApplicationDbContext context, int activeCount, int archivedCount)
        {
            #region implementation
            for (var i = 0; i < activeCount; i++)
            {
                context.SplData.Add(new SplData($"<document>active {i}</document>", Guid.NewGuid()));
            }

            for (var i = 0; i < archivedCount; i++)
            {
                context.SplData.Add(new SplData($"<document>archived {i}</document>", Guid.NewGuid()) { Archive = true });
            }

            context.SaveChanges();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Computes the expected SHA-256 hex hash using the service's
        /// normalization rules (trim plus newline normalization).
        /// </summary>
        /// <param name="xmlContent">Raw XML content.</param>
        /// <returns>Lowercase hex hash string.</returns>
        /// <seealso cref="SplDataService.CreateSplDataAsync"/>
        private static string computeExpectedHash(string xmlContent)
        {
            #region implementation
            var normalized = xmlContent.Trim()
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");

            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalized));

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
            #endregion
        }

        #endregion
    }
}
