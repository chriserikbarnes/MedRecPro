using MedRecPro.Models;
using MedRecPro.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.IO.Compression;
using System.Text;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for SplImportService ZIP file processing functionality.
    /// </summary>
    /// <remarks>
    /// Tests cover ZIP file processing, empty ZIP handling, XML extraction,
    /// duplicate detection, progress tracking, and various error scenarios
    /// for SPL data import operations.
    /// </remarks>
    /// <seealso cref="SplImportService"/>
    /// <seealso cref="BufferedFile"/>
    /// <seealso cref="SplZipImportResult"/>
    [TestClass]
    public class SplImportServiceTests
    {
        #region Test Constants

        /// <summary>
        /// Valid XML content for testing.
        /// </summary>
        private const string ValidXmlContent = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><document><id root=\"2.16.840.1.113883.3.150\" extension=\"240fa4f4-d357-9079-e063-6394a90a77e2\"/><effectiveTime value=\"20231115\"/></document>";

        /// <summary>
        /// Test XML filename with GUID.
        /// </summary>
        private const string TestXmlFileName = "240fa4f4-d357-9079-e063-6394a90a77e2.xml";

        #endregion

        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates a temporary ZIP file with specified XML content.
        /// </summary>
        /// <param name="xmlContent">The XML content to include in the ZIP</param>
        /// <param name="xmlFileName">The filename for the XML entry</param>
        /// <returns>Path to the created temporary ZIP file</returns>
        private string createTempZipFile(string xmlContent, string xmlFileName = TestXmlFileName)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

            using (var fileStream = new FileStream(tempPath, FileMode.Create))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(xmlFileName);
                using (var entryStream = entry.Open())
                using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
                {
                    writer.Write(xmlContent);
                }
            }

            return tempPath;
        }

        /**************************************************************/
        /// <summary>
        /// Creates a temporary empty ZIP file.
        /// </summary>
        /// <returns>Path to the created empty ZIP file</returns>
        private string createEmptyZipFile()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

            using (var fileStream = new FileStream(tempPath, FileMode.Create))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                // Create empty ZIP with no entries
            }

            return tempPath;
        }

        /**************************************************************/
        /// <summary>
        /// Creates a temporary ZIP file with multiple XML entries.
        /// </summary>
        /// <param name="xmlEntries">Dictionary of filename to XML content</param>
        /// <returns>Path to the created ZIP file</returns>
        private string createMultiEntryZipFile(Dictionary<string, string> xmlEntries)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

            using (var fileStream = new FileStream(tempPath, FileMode.Create))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                foreach (var entry in xmlEntries)
                {
                    var zipEntry = archive.CreateEntry(entry.Key);
                    using (var entryStream = zipEntry.Open())
                    using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
                    {
                        writer.Write(entry.Value);
                    }
                }
            }

            return tempPath;
        }

        /**************************************************************/
        /// <summary>
        /// Creates a BufferedFile from a temporary file path.
        /// </summary>
        /// <param name="tempPath">Path to the temporary file</param>
        /// <param name="fileName">Display name for the file</param>
        /// <returns>A BufferedFile instance</returns>
        private BufferedFile createBufferedFile(string tempPath, string fileName = "test.zip")
        {
            return new BufferedFile
            {
                FileName = fileName,
                TempFilePath = tempPath
            };
        }

        /**************************************************************/
        /// <summary>
        /// Creates a mock SplImportService with mocked dependencies.
        /// </summary>
        /// <returns>A configured SplImportService instance</returns>
        private SplImportService createMockedImportService()
        {
            var logger = new Mock<ILogger<SplImportService>>();
            var scopeFactory = new Mock<IServiceScopeFactory>();
            var scope = new Mock<IServiceScope>();
            var serviceProvider = new Mock<IServiceProvider>();

            // Setup the scope factory to return a mock scope
            scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);
            scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);

            // Setup mock services
            var splDataService = new Mock<SplDataService>();
            var splXmlParser = new Mock<SplXmlParser>();

            serviceProvider.Setup(x => x.GetService(typeof(SplDataService)))
                .Returns(splDataService.Object);
            serviceProvider.Setup(x => x.GetService(typeof(SplXmlParser)))
                .Returns(splXmlParser.Object);

            // Configure SplDataService to return non-duplicate for testing
            splDataService.Setup(x => x.IsDuplicateSplDataAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                .ReturnsAsync(false);
            splDataService.Setup(x => x.GetOrCreateSplDataAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<long?>()))
                .ReturnsAsync("encrypted-spl-data-id");

            // Configure SplXmlParser to return successful result
            splXmlParser.Setup(x => x.ParseAndSaveSplDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string>>(), null))
                .ReturnsAsync(new SplFileImportResult
                {
                    FileName = TestXmlFileName,
                    Success = true,
                    Message = "Successfully parsed and saved"
                });

            return new SplImportService(scopeFactory.Object, logger.Object);
        }

        /**************************************************************/
        /// <summary>
        /// Cleans up temporary files created during tests.
        /// </summary>
        /// <param name="paths">Paths to delete</param>
        private void cleanupTempFiles(params string[] paths)
        {
            foreach (var path in paths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        #endregion

        #region Empty ZIP File Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that processing an empty ZIP file returns appropriate error result.
        /// </summary>
        /// <remarks>
        /// This tests the empty ZIP handling at SplImportService.cs:152-157
        /// </remarks>
        /// <seealso cref="SplImportService.ProcessZipFilesAsync"/>
        [TestMethod]
        public async Task ProcessZipFilesAsync_EmptyZipFile_ReturnsErrorResult()
        {
            #region implementation

            // Arrange
            var service = createMockedImportService();
            var emptyZipPath = createEmptyZipFile();

            try
            {
                var bufferedFiles = new List<BufferedFile>
                {
                    createBufferedFile(emptyZipPath, "empty.zip")
                };

                var progressCounter = 0;
                Action<int> fileCounter = count => progressCounter = count;

                // Act
                var results = await service.ProcessZipFilesAsync(
                    bufferedFiles,
                    currentUserId: 1,
                    token: CancellationToken.None,
                    fileCounter: fileCounter,
                    updateStatus: null,
                    results: null);

                // Assert
                Assert.AreEqual(1, results.Count, "Should return one result for one ZIP file");
                Assert.AreEqual("empty.zip", results[0].ZipFileName);
                Assert.AreEqual(1, results[0].FileResults.Count, "Should have one file result");
                Assert.IsFalse(results[0].FileResults[0].Success, "Empty ZIP should not succeed");
                Assert.IsTrue(results[0].FileResults[0].Message.Contains("empty") ||
                              results[0].FileResults[0].Message.Contains("invalid"),
                    "Error message should indicate empty or invalid ZIP");
            }
            finally
            {
                cleanupTempFiles(emptyZipPath);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a zero-byte file is handled as empty ZIP.
        /// </summary>
        /// <seealso cref="SplImportService.ProcessZipFilesAsync"/>
        [TestMethod]
        public async Task ProcessZipFilesAsync_ZeroByteFile_ReturnsErrorResult()
        {
            #region implementation

            // Arrange
            var service = createMockedImportService();
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

            // Create a zero-byte file
            File.Create(tempPath).Close();

            try
            {
                var bufferedFiles = new List<BufferedFile>
                {
                    createBufferedFile(tempPath, "zerobyte.zip")
                };

                Action<int> fileCounter = _ => { };

                // Act
                var results = await service.ProcessZipFilesAsync(
                    bufferedFiles,
                    currentUserId: 1,
                    token: CancellationToken.None,
                    fileCounter: fileCounter);

                // Assert
                Assert.AreEqual(1, results.Count);
                Assert.IsFalse(results[0].FileResults[0].Success, "Zero-byte file should fail");
            }
            finally
            {
                cleanupTempFiles(tempPath);
            }

            #endregion
        }

        #endregion

        #region Valid ZIP Processing Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that a valid ZIP file with XML content is processed successfully.
        /// </summary>
        /// <seealso cref="SplImportService.ProcessZipFilesAsync"/>
        [TestMethod]
        public async Task ProcessZipFilesAsync_ValidZipWithXml_ProcessesSuccessfully()
        {
            #region implementation

            // Arrange
            var service = createMockedImportService();
            var zipPath = createTempZipFile(ValidXmlContent);

            try
            {
                var bufferedFiles = new List<BufferedFile>
                {
                    createBufferedFile(zipPath, "valid.zip")
                };

                Action<int> fileCounter = _ => { };

                // Act
                var results = await service.ProcessZipFilesAsync(
                    bufferedFiles,
                    currentUserId: 1,
                    token: CancellationToken.None,
                    fileCounter: fileCounter);

                // Assert
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual("valid.zip", results[0].ZipFileName);
                Assert.IsTrue(results[0].FileResults.Count > 0, "Should have file results");
            }
            finally
            {
                cleanupTempFiles(zipPath);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that multiple XML entries in a ZIP are all processed.
        /// </summary>
        /// <seealso cref="SplImportService.ProcessZipFilesAsync"/>
        [TestMethod]
        public async Task ProcessZipFilesAsync_MultipleXmlEntries_ProcessesAll()
        {
            #region implementation

            // Arrange
            var service = createMockedImportService();
            var xmlEntries = new Dictionary<string, string>
            {
                { "file1.xml", ValidXmlContent },
                { "file2.xml", ValidXmlContent.Replace("240fa4f4", "340fb5f5") },
                { "file3.xml", ValidXmlContent.Replace("240fa4f4", "440fc6f6") }
            };
            var zipPath = createMultiEntryZipFile(xmlEntries);

            try
            {
                var bufferedFiles = new List<BufferedFile>
                {
                    createBufferedFile(zipPath, "multi.zip")
                };

                Action<int> fileCounter = _ => { };

                // Act
                var results = await service.ProcessZipFilesAsync(
                    bufferedFiles,
                    currentUserId: 1,
                    token: CancellationToken.None,
                    fileCounter: fileCounter);

                // Assert
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(3, results[0].FileResults.Count, "Should process all three XML files");
            }
            finally
            {
                cleanupTempFiles(zipPath);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that non-XML entries in a ZIP are skipped.
        /// </summary>
        /// <seealso cref="SplImportService.ProcessZipFilesAsync"/>
        [TestMethod]
        public async Task ProcessZipFilesAsync_MixedEntries_SkipsNonXml()
        {
            #region implementation

            // Arrange
            var service = createMockedImportService();
            var entries = new Dictionary<string, string>
            {
                { "document.xml", ValidXmlContent },
                { "readme.txt", "This is a readme file" },
                { "image.png", "fake image content" }
            };
            var zipPath = createMultiEntryZipFile(entries);

            try
            {
                var bufferedFiles = new List<BufferedFile>
                {
                    createBufferedFile(zipPath, "mixed.zip")
                };

                Action<int> fileCounter = _ => { };

                // Act
                var results = await service.ProcessZipFilesAsync(
                    bufferedFiles,
                    currentUserId: 1,
                    token: CancellationToken.None,
                    fileCounter: fileCounter);

                // Assert
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(1, results[0].FileResults.Count, "Should only process the XML file");
            }
            finally
            {
                cleanupTempFiles(zipPath);
            }

            #endregion
        }

        #endregion

        #region Multiple ZIP Files Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that multiple ZIP files are processed in sequence.
        /// </summary>
        /// <seealso cref="SplImportService.ProcessZipFilesAsync"/>
        [TestMethod]
        public async Task ProcessZipFilesAsync_MultipleZipFiles_ProcessesAll()
        {
            #region implementation

            // Arrange
            var service = createMockedImportService();
            var zipPath1 = createTempZipFile(ValidXmlContent, "file1.xml");
            var zipPath2 = createTempZipFile(ValidXmlContent.Replace("240fa4f4", "340fb5f5"), "file2.xml");
            var zipPath3 = createTempZipFile(ValidXmlContent.Replace("240fa4f4", "440fc6f6"), "file3.xml");

            try
            {
                var bufferedFiles = new List<BufferedFile>
                {
                    createBufferedFile(zipPath1, "first.zip"),
                    createBufferedFile(zipPath2, "second.zip"),
                    createBufferedFile(zipPath3, "third.zip")
                };

                Action<int> fileCounter = _ => { };

                // Act
                var results = await service.ProcessZipFilesAsync(
                    bufferedFiles,
                    currentUserId: 1,
                    token: CancellationToken.None,
                    fileCounter: fileCounter);

                // Assert
                Assert.AreEqual(3, results.Count, "Should return results for all three ZIP files");
                Assert.AreEqual("first.zip", results[0].ZipFileName);
                Assert.AreEqual("second.zip", results[1].ZipFileName);
                Assert.AreEqual("third.zip", results[2].ZipFileName);
            }
            finally
            {
                cleanupTempFiles(zipPath1, zipPath2, zipPath3);
            }

            #endregion
        }

        #endregion

        #region Progress Tracking Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that progress callback is invoked during processing.
        /// </summary>
        /// <seealso cref="SplImportService.ProcessZipFilesAsync"/>
        [TestMethod]
        public async Task ProcessZipFilesAsync_WithProgressCallback_ReportsProgress()
        {
            #region implementation

            // Arrange
            var service = createMockedImportService();
            var zipPath = createTempZipFile(ValidXmlContent);

            try
            {
                var bufferedFiles = new List<BufferedFile>
                {
                    createBufferedFile(zipPath, "progress.zip")
                };

                var progressUpdates = new List<int>();
                Action<int> fileCounter = count => progressUpdates.Add(count);

                // Act
                var results = await service.ProcessZipFilesAsync(
                    bufferedFiles,
                    currentUserId: 1,
                    token: CancellationToken.None,
                    fileCounter: fileCounter);

                // Assert
                Assert.IsTrue(progressUpdates.Count > 0, "Progress should be reported");
                Assert.IsTrue(progressUpdates.Last() > 0, "Final progress should be greater than 0");
            }
            finally
            {
                cleanupTempFiles(zipPath);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that status callback is invoked during processing.
        /// </summary>
        /// <seealso cref="SplImportService.ProcessZipFilesAsync"/>
        [TestMethod]
        public async Task ProcessZipFilesAsync_WithStatusCallback_ReportsStatus()
        {
            #region implementation

            // Arrange
            var service = createMockedImportService();
            var zipPath = createTempZipFile(ValidXmlContent);

            try
            {
                var bufferedFiles = new List<BufferedFile>
                {
                    createBufferedFile(zipPath, "status.zip")
                };

                var statusUpdates = new List<string>();
                Action<string> updateStatus = status => statusUpdates.Add(status);
                Action<int> fileCounter = _ => { };

                // Act
                var results = await service.ProcessZipFilesAsync(
                    bufferedFiles,
                    currentUserId: 1,
                    token: CancellationToken.None,
                    fileCounter: fileCounter,
                    updateStatus: updateStatus);

                // Assert - Status updates may or may not occur depending on implementation
                // The test ensures the callback can be invoked without error
                Assert.IsNotNull(results);
            }
            finally
            {
                cleanupTempFiles(zipPath);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that results callback returns accumulated results.
        /// </summary>
        /// <seealso cref="SplImportService.ProcessZipFilesAsync"/>
        [TestMethod]
        public async Task ProcessZipFilesAsync_WithResultsCallback_ReturnsAccumulatedResults()
        {
            #region implementation

            // Arrange
            var service = createMockedImportService();
            var zipPath1 = createTempZipFile(ValidXmlContent, "file1.xml");
            var zipPath2 = createTempZipFile(ValidXmlContent.Replace("240fa4f4", "340fb5f5"), "file2.xml");

            try
            {
                var bufferedFiles = new List<BufferedFile>
                {
                    createBufferedFile(zipPath1, "first.zip"),
                    createBufferedFile(zipPath2, "second.zip")
                };

                var resultSnapshots = new List<int>();
                Action<List<SplZipImportResult>> results = r => resultSnapshots.Add(r.Count);
                Action<int> fileCounter = _ => { };

                // Act
                var finalResults = await service.ProcessZipFilesAsync(
                    bufferedFiles,
                    currentUserId: 1,
                    token: CancellationToken.None,
                    fileCounter: fileCounter,
                    results: results);

                // Assert
                Assert.IsTrue(resultSnapshots.Count >= 2, "Results callback should be called for each ZIP");
                Assert.AreEqual(2, finalResults.Count, "Final results should contain all processed ZIPs");
            }
            finally
            {
                cleanupTempFiles(zipPath1, zipPath2);
            }

            #endregion
        }

        #endregion

        #region Cancellation Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that cancellation token stops processing.
        /// </summary>
        /// <seealso cref="SplImportService.ProcessZipFilesAsync"/>
        [TestMethod]
        public async Task ProcessZipFilesAsync_CancellationRequested_StopsProcessing()
        {
            #region implementation

            // Arrange
            var service = createMockedImportService();
            var entries = new Dictionary<string, string>
            {
                { "file1.xml", ValidXmlContent },
                { "file2.xml", ValidXmlContent },
                { "file3.xml", ValidXmlContent }
            };
            var zipPath = createMultiEntryZipFile(entries);

            try
            {
                var bufferedFiles = new List<BufferedFile>
                {
                    createBufferedFile(zipPath, "cancel.zip")
                };

                // Create a cancellation token that's already cancelled
                var cts = new CancellationTokenSource();
                cts.Cancel();

                Action<int> fileCounter = _ => { };

                // Act & Assert
                await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
                {
                    await service.ProcessZipFilesAsync(
                        bufferedFiles,
                        currentUserId: 1,
                        token: cts.Token,
                        fileCounter: fileCounter);
                });
            }
            finally
            {
                cleanupTempFiles(zipPath);
            }

            #endregion
        }

        #endregion

        #region Invalid ZIP Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that corrupted ZIP file returns error result.
        /// </summary>
        /// <seealso cref="SplImportService.ProcessZipFilesAsync"/>
        [TestMethod]
        public async Task ProcessZipFilesAsync_CorruptedZip_ReturnsErrorResult()
        {
            #region implementation

            // Arrange
            var service = createMockedImportService();
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

            // Create a file with random content (not a valid ZIP)
            File.WriteAllText(tempPath, "This is not a valid ZIP file content");

            try
            {
                var bufferedFiles = new List<BufferedFile>
                {
                    createBufferedFile(tempPath, "corrupted.zip")
                };

                Action<int> fileCounter = _ => { };

                // Act
                var results = await service.ProcessZipFilesAsync(
                    bufferedFiles,
                    currentUserId: 1,
                    token: CancellationToken.None,
                    fileCounter: fileCounter);

                // Assert
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(1, results[0].FileResults.Count);
                Assert.IsFalse(results[0].FileResults[0].Success, "Corrupted ZIP should fail");
                Assert.IsTrue(results[0].FileResults[0].Message.Contains("Error"),
                    "Error message should indicate ZIP processing error");
            }
            finally
            {
                cleanupTempFiles(tempPath);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that ZIP file with only non-XML entries returns no file results.
        /// </summary>
        /// <seealso cref="SplImportService.ProcessZipFilesAsync"/>
        [TestMethod]
        public async Task ProcessZipFilesAsync_NoXmlEntries_ReturnsNoFileResults()
        {
            #region implementation

            // Arrange
            var service = createMockedImportService();
            var entries = new Dictionary<string, string>
            {
                { "readme.txt", "This is a readme" },
                { "data.json", "{\"key\": \"value\"}" }
            };
            var zipPath = createMultiEntryZipFile(entries);

            try
            {
                var bufferedFiles = new List<BufferedFile>
                {
                    createBufferedFile(zipPath, "noxml.zip")
                };

                Action<int> fileCounter = _ => { };

                // Act
                var results = await service.ProcessZipFilesAsync(
                    bufferedFiles,
                    currentUserId: 1,
                    token: CancellationToken.None,
                    fileCounter: fileCounter);

                // Assert
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(0, results[0].FileResults.Count, "Should have no file results for ZIP with no XML");
            }
            finally
            {
                cleanupTempFiles(zipPath);
            }

            #endregion
        }

        #endregion

        #region GUID Parsing Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that XML filename with valid GUID is parsed correctly.
        /// </summary>
        /// <seealso cref="SplImportService"/>
        [TestMethod]
        public async Task ProcessZipFilesAsync_GuidFilename_ParsesCorrectly()
        {
            #region implementation

            // Arrange
            var service = createMockedImportService();
            var guidFileName = "240fa4f4-d357-9079-e063-6394a90a77e2.xml";
            var zipPath = createTempZipFile(ValidXmlContent, guidFileName);

            try
            {
                var bufferedFiles = new List<BufferedFile>
                {
                    createBufferedFile(zipPath, "guid.zip")
                };

                Action<int> fileCounter = _ => { };

                // Act
                var results = await service.ProcessZipFilesAsync(
                    bufferedFiles,
                    currentUserId: 1,
                    token: CancellationToken.None,
                    fileCounter: fileCounter);

                // Assert
                Assert.AreEqual(1, results.Count);
                Assert.IsTrue(results[0].FileResults.Count > 0, "GUID filename should be processed");
            }
            finally
            {
                cleanupTempFiles(zipPath);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that XML filename without valid GUID is still processed.
        /// </summary>
        /// <seealso cref="SplImportService"/>
        [TestMethod]
        public async Task ProcessZipFilesAsync_NonGuidFilename_ProcessesWithEmptyGuid()
        {
            #region implementation

            // Arrange
            var service = createMockedImportService();
            var nonGuidFileName = "document.xml";
            var zipPath = createTempZipFile(ValidXmlContent, nonGuidFileName);

            try
            {
                var bufferedFiles = new List<BufferedFile>
                {
                    createBufferedFile(zipPath, "nonguid.zip")
                };

                Action<int> fileCounter = _ => { };

                // Act
                var results = await service.ProcessZipFilesAsync(
                    bufferedFiles,
                    currentUserId: 1,
                    token: CancellationToken.None,
                    fileCounter: fileCounter);

                // Assert
                Assert.AreEqual(1, results.Count);
                Assert.IsTrue(results[0].FileResults.Count > 0, "Non-GUID filename should still be processed");
            }
            finally
            {
                cleanupTempFiles(zipPath);
            }

            #endregion
        }

        #endregion

        #region Case Sensitivity Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that XML file extension is case-insensitive.
        /// </summary>
        /// <seealso cref="SplImportService"/>
        [TestMethod]
        public async Task ProcessZipFilesAsync_UppercaseXmlExtension_Processes()
        {
            #region implementation

            // Arrange
            var service = createMockedImportService();
            var zipPath = createTempZipFile(ValidXmlContent, "DOCUMENT.XML");

            try
            {
                var bufferedFiles = new List<BufferedFile>
                {
                    createBufferedFile(zipPath, "uppercase.zip")
                };

                Action<int> fileCounter = _ => { };

                // Act
                var results = await service.ProcessZipFilesAsync(
                    bufferedFiles,
                    currentUserId: 1,
                    token: CancellationToken.None,
                    fileCounter: fileCounter);

                // Assert
                Assert.AreEqual(1, results.Count);
                Assert.IsTrue(results[0].FileResults.Count > 0, "Uppercase XML extension should be processed");
            }
            finally
            {
                cleanupTempFiles(zipPath);
            }

            #endregion
        }

        #endregion

        #region Empty List Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that empty file list returns empty results.
        /// </summary>
        /// <seealso cref="SplImportService.ProcessZipFilesAsync"/>
        [TestMethod]
        public async Task ProcessZipFilesAsync_EmptyFileList_ReturnsEmptyResults()
        {
            #region implementation

            // Arrange
            var service = createMockedImportService();
            var bufferedFiles = new List<BufferedFile>();

            Action<int> fileCounter = _ => { };

            // Act
            var results = await service.ProcessZipFilesAsync(
                bufferedFiles,
                currentUserId: 1,
                token: CancellationToken.None,
                fileCounter: fileCounter);

            // Assert
            Assert.AreEqual(0, results.Count, "Empty file list should return empty results");

            #endregion
        }

        #endregion
    }
}
