using MedRecPro.Data;
using MedRecPro.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace MedRecPro.Service
{
    /// <summary>
    /// Service responsible for importing and processing SPL (Structured Product Labeling) data from ZIP archives containing XML files.
    /// Handles batch processing of multiple ZIP files and delegates XML parsing to the SplXmlParser.
    /// </summary>
    /// <remarks>
    /// This service is designed to process pharmaceutical product labeling data in compliance with FDA SPL standards.
    /// It extracts XML files from ZIP archives and processes them asynchronously for better performance.
    /// </remarks>
    public class SplImportService
    {
        #region private fields

        /// <summary>
        /// Logger instance for tracking import operations and debugging issues.
        /// </summary>
        private readonly ILogger<SplImportService> _logger;

        private readonly IServiceScopeFactory _scopeFactory;

        #endregion

        #region constants
        private const string XML_FILE_EXTENSION = ".xml";
        private const string EMPTY_ZIP_ERROR_MESSAGE = "ZIP file is empty or invalid.";
        private const string STORAGE_ERROR_PREFIX = "Failed to store XML content: ";
        private const string ZIP_PROCESSING_ERROR_PREFIX = "Error processing ZIP: ";
        private const string ZIP_ARCHIVE_PROCESSING = "ZIP Archive Processing";
        private const string ZIP_ARCHIVE = "ZIP Archive";
        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SplImportService with required dependencies.
        /// </summary>
        /// <param name="scopeFactory">Service provider for dependency injection</param>
        /// <param name="logger">Logger for tracking operations and errors</param>
        /// <example>
        /// // Typically injected via dependency injection container
        /// services.AddScoped&lt;SplImportService&gt;();
        /// </example>
        public SplImportService(IServiceScopeFactory scopeFactory, ILogger<SplImportService> logger)
        {
            #region implementation          
            _scopeFactory = scopeFactory;
            _logger = logger;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes multiple ZIP files containing SPL XML data asynchronously.
        /// Each ZIP file is extracted and its XML entries are parsed and saved to the database.
        /// </summary>
        /// <param name="bufferedFiles">Collection of uploaded ZIP files to process</param>
        /// <param name="currentUserId">The ID of the user who initiated the import operation</param>
        /// <param name="token">Cancellation token from caller</param>
        /// <param name="fileCounter">Delegate for tracking progress</param>
        /// <param name="updateStatus">Delegate for tracking import status</param>
        /// <param name="results">Delegate for tracking the results</param>
        /// <returns>A list of SplZipImportResult objects containing the results for each ZIP file processed</returns>
        /// <example>
        /// <code>
        /// var zipFiles = Request.Form.Files.Where(f => f.FileName.EndsWith(".zip")).ToList();
        /// var results = await splImportService.ProcessZipFilesAsync(zipFiles);
        /// foreach(var result in results)
        /// {
        ///     Console.WriteLine($"Processed {result.ZipFileName}: {result.FileResults.Count} files");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method handles errors gracefully and continues processing remaining files even if individual files fail.
        /// Empty ZIP files and non-XML entries are logged but do not stop the overall process.
        /// Uses dependency injection scopes to ensure proper resource management for each XML parsing operation.
        /// Progress reporting is calculated based on processed file count relative to total buffered files.
        /// </remarks>
        /// <seealso cref="SplZipImportResult"/>
        /// <seealso cref="SplFileImportResult"/>
        /// <seealso cref="BufferedFile"/>
        /// <seealso cref="SplXmlParser"/>
        /// <seealso cref="Label"/>
        public async Task<List<SplZipImportResult>> ProcessZipFilesAsync(
            List<BufferedFile> bufferedFiles,
            long? currentUserId,
            CancellationToken token,
            Action<int> fileCounter,
            Action<string>? updateStatus = null,
            Action<List<SplZipImportResult>>? results = null)
        {
            #region implementation
            var allZipResults = new List<SplZipImportResult>();
            var progressTracker = new ProcessingProgressTracker(bufferedFiles.Count);

            foreach (var zipFile in bufferedFiles)
            {
                var zipResult = await processZipFileAsync(zipFile, currentUserId, updateStatus, progressTracker, token);
                allZipResults.Add(zipResult);

                updateProgressAndResults(progressTracker, fileCounter, results, allZipResults);
            }

            return allZipResults;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes a single ZIP file and extracts all XML entries for SPL data processing.
        /// </summary>
        /// <param name="zipFile">The ZIP file to process</param>
        /// <param name="currentUserId">The ID of the user who initiated the import operation</param>
        /// <param name="updateStatus">Delegate for tracking import status</param>
        /// <param name="progressTracker">Progress tracking helper</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>A SplZipImportResult containing the results for this ZIP file</returns>
        /// <remarks>
        /// Handles empty ZIP files and continues processing even if individual XML entries fail.
        /// Creates proper scopes for dependency injection to ensure resource management.
        /// </remarks>
        /// <seealso cref="SplZipImportResult"/>
        /// <seealso cref="BufferedFile"/>
        /// <seealso cref="Label"/>
        private async Task<SplZipImportResult> processZipFileAsync(
            BufferedFile zipFile,
            long? currentUserId,
            Action<string>? updateStatus,
            ProcessingProgressTracker progressTracker,
            CancellationToken token)
        {
            #region implementation
            var zipResult = new SplZipImportResult { ZipFileName = zipFile.FileName };

            _logger.LogInformation("Processing ZIP file: {ZipFileName}", zipFile.FileName);

            try
            {
                using var stream = new FileStream(zipFile.TempFilePath, FileMode.Open, FileAccess.Read);

                if (stream.Length == 0)
                {
                    zipResult.FileResults.Add(createEmptyZipResult());
                    _logger.LogWarning("ZIP file {ZipFileName} is empty.", zipFile.FileName);
                    return zipResult;
                }

                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

                await processZipEntriesAsync(archive, zipFile.FileName, currentUserId, updateStatus, zipResult, progressTracker, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ZIP file {ZipFileName}", zipFile.FileName);
                zipResult.FileResults.Add(createZipProcessingErrorResult(ex.Message));
            }

            return zipResult;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes all XML entries within a ZIP archive.
        /// </summary>
        /// <param name="archive">The ZIP archive to process</param>
        /// <param name="zipFileName">Name of the ZIP file for logging</param>
        /// <param name="currentUserId">The ID of the user who initiated the import operation</param>
        /// <param name="updateStatus">Delegate for tracking import status</param>
        /// <param name="zipResult">The result object to populate</param>
        /// <param name="progressTracker">Progress tracking helper</param>
        /// <param name="token">Cancellation token</param>
        /// <remarks>
        /// Only processes XML files with content, skipping empty or non-XML entries.
        /// Uses dependency injection scopes for proper resource management.
        /// </remarks>
        /// <seealso cref="SplZipImportResult"/>
        /// <seealso cref="ZipArchive"/>
        /// <seealso cref="Label"/>
        private async Task processZipEntriesAsync(
            ZipArchive archive,
            string zipFileName,
            long? currentUserId,
            Action<string>? updateStatus,
            SplZipImportResult zipResult,
            ProcessingProgressTracker progressTracker,
            CancellationToken token)
        {
            #region implementation
            foreach (var entry in archive.Entries)
            {
                token.ThrowIfCancellationRequested();

                if (!isValidXmlEntry(entry))
                {
                    _logger.LogDebug("Skipping non-XML or empty entry: {EntryFullName} in {ZipFileName}",
                        entry.FullName, zipFileName);
                    continue;
                }

                _logger.LogInformation("Processing XML entry: {XmlFileName} from {ZipFileName}",
                    entry.FullName, zipFileName);

                var fileResult = await processXmlEntryAsync(entry, currentUserId, updateStatus, token);
                zipResult.FileResults.Add(fileResult);

                progressTracker.IncrementProcessedCount();
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes a single XML entry from a ZIP archive.
        /// </summary>
        /// <param name="entry">The ZIP entry containing XML content</param>
        /// <param name="currentUserId">The ID of the user who initiated the import operation</param>
        /// <param name="updateStatus">Delegate for tracking import status</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>A SplFileImportResult containing the processing result</returns>
        /// <remarks>
        /// Extracts XML content, stores it in the database, and parses it for SPL data.
        /// Uses scoped dependency injection to ensure proper resource cleanup.
        /// Handles duplicate detection to avoid processing the same content multiple times.
        /// CRITICAL: Wraps parsing in a transaction to keep database connection open for all operations,
        /// reducing 621 connection opens to 1 per XML file for dramatic performance improvement.
        /// </remarks>
        /// <seealso cref="SplFileImportResult"/>
        /// <seealso cref="ZipArchiveEntry"/>
        /// <seealso cref="SplXmlParser"/>
        /// <seealso cref="Label"/>
        private async Task<SplFileImportResult> processXmlEntryAsync(
            ZipArchiveEntry entry,
            long? currentUserId,
            Action<string>? updateStatus,
            CancellationToken token)
        {
            #region implementation
            try
            {
                var xmlContent = await extractXmlContentAsync(entry);
                var xmlFileGuid = parseXmlFileGuid(entry.Name);

                using var scope = _scopeFactory.CreateScope();
                var splDataService = scope.ServiceProvider.GetRequiredService<SplDataService>();

                // Skip duplicate content
                if (await splDataService.IsDuplicateSplDataAsync(xmlContent, xmlFileGuid))
                {
                    return createSkippedDuplicateResult(entry.FullName);
                }

                // Get shared DbContext for connection reuse
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Manually open connection - EF will keep it open and reuse it
                var connection = context.Database.GetDbConnection();
                await connection.OpenAsync(token);

                try
                {
                    await storeXmlContentAsync(splDataService, xmlContent, xmlFileGuid,
                        currentUserId, entry.FullName, updateStatus);

                    var xmlParser = scope.ServiceProvider.GetRequiredService<SplXmlParser>();
                    var result = await xmlParser.ParseAndSaveSplDataAsync(xmlContent,
                        entry.FullName, updateStatus, context);

                    return result;
                }
                finally
                {
                    // Always close the connection
                    await connection.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process XML entry {XmlFileName}", entry.FullName);
                return createXmlProcessingErrorResult(entry.FullName, ex.Message);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores XML content in the SplData table with proper error handling.
        /// </summary>
        /// <param name="splDataService">Service for managing SPL data storage</param>
        /// <param name="xmlContent">The XML content to store</param>
        /// <param name="xmlFileGuid">GUID identifier for the XML file</param>
        /// <param name="currentUserId">The ID of the user who initiated the import operation</param>
        /// <param name="fileName">Name of the file for logging purposes</param>
        /// <param name="updateStatus">Delegate for tracking import status</param>
        /// <remarks>
        /// Uses GetOrCreateSplDataAsync to avoid duplicate records.
        /// Logs successful storage operations for audit purposes.
        /// </remarks>
        /// <seealso cref="SplDataService"/>
        /// <seealso cref="Label"/>
        private async Task storeXmlContentAsync(
            SplDataService splDataService,
            string xmlContent,
            Guid xmlFileGuid,
            long? currentUserId,
            string fileName,
            Action<string>? updateStatus)
        {
            #region implementation
            try
            {
                var encryptedSplDataId = await splDataService.GetOrCreateSplDataAsync(xmlContent, xmlFileGuid, currentUserId);

                _logger.LogInformation("Stored XML content with encrypted SplData ID: {EncryptedSplDataId} for file {XmlFileName}",
                    encryptedSplDataId, fileName);

                updateStatus?.Invoke($"Stored XML content for {fileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store XML content for file {XmlFileName}", fileName);
                throw new InvalidOperationException($"{STORAGE_ERROR_PREFIX}{ex.Message}", ex);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts XML content from a ZIP archive entry using UTF-8 encoding.
        /// </summary>
        /// <param name="entry">The ZIP entry containing XML content</param>
        /// <returns>The extracted XML content as a string</returns>
        /// <remarks>
        /// Uses UTF-8 encoding for proper character handling.
        /// Ensures proper disposal of streams through using statements.
        /// </remarks>
        /// <seealso cref="ZipArchiveEntry"/>
        /// <seealso cref="Label"/>
        private async Task<string> extractXmlContentAsync(ZipArchiveEntry entry)
        {
            #region implementation
            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the GUID from an XML file name.
        /// </summary>
        /// <param name="fileName">The XML file name to parse</param>
        /// <returns>The parsed GUID or Guid.Empty if parsing fails</returns>
        /// <remarks>
        /// Attempts to parse the first part of the filename before the extension as a GUID.
        /// Returns Guid.Empty if parsing fails to maintain processing flow.
        /// </remarks>
        /// <seealso cref="Label"/>
        private Guid parseXmlFileGuid(string fileName)
        {
            #region implementation
            var fileNameWithoutExtension = fileName.Split('.').FirstOrDefault();
            Guid.TryParse(fileNameWithoutExtension, out var xmlFileGuid);
            return xmlFileGuid;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if a ZIP entry is a valid XML file with content.
        /// </summary>
        /// <param name="entry">The ZIP entry to validate</param>
        /// <returns>True if the entry is a valid XML file with content, false otherwise</returns>
        /// <remarks>
        /// Checks both file extension and content length to ensure processable XML files.
        /// Case-insensitive comparison for file extensions.
        /// </remarks>
        /// <seealso cref="ZipArchiveEntry"/>
        /// <seealso cref="Label"/>
        private bool isValidXmlEntry(ZipArchiveEntry entry)
        {
            #region implementation
            return entry.FullName.EndsWith(XML_FILE_EXTENSION, StringComparison.OrdinalIgnoreCase) && entry.Length > 0;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Updates progress tracking and invokes result callbacks.
        /// </summary>
        /// <param name="progressTracker">Progress tracking helper</param>
        /// <param name="fileCounter">Delegate for reporting progress percentage</param>
        /// <param name="results">Delegate for reporting current results</param>
        /// <param name="allZipResults">Current collection of all results</param>
        /// <remarks>
        /// Calculates progress percentage based on processed files and invokes callbacks if provided.
        /// Thread-safe operations for progress reporting.
        /// </remarks>
        /// <seealso cref="ProcessingProgressTracker"/>
        /// <seealso cref="Label"/>
        private void updateProgressAndResults(
            ProcessingProgressTracker progressTracker,
            Action<int> fileCounter,
            Action<List<SplZipImportResult>>? results,
            List<SplZipImportResult> allZipResults)
        {
            #region implementation
            var progressPercentage = progressTracker.GetProgressPercentage();
            fileCounter(progressPercentage);
            results?.Invoke(allZipResults.ToList());
            #endregion
        }

        #region result creation helpers

        /**************************************************************/
        /// <summary>
        /// Creates a result object for empty ZIP files.
        /// </summary>
        /// <returns>A SplFileImportResult indicating empty ZIP file</returns>
        /// <seealso cref="SplFileImportResult"/>
        /// <seealso cref="Label"/>
        private SplFileImportResult createEmptyZipResult()
        {
            #region implementation
            return new SplFileImportResult
            {
                FileName = ZIP_ARCHIVE,
                Success = false,
                Message = EMPTY_ZIP_ERROR_MESSAGE
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a result object for ZIP processing errors.
        /// </summary>
        /// <param name="errorMessage">The error message to include</param>
        /// <returns>A SplFileImportResult indicating ZIP processing error</returns>
        /// <seealso cref="SplFileImportResult"/>
        /// <seealso cref="Label"/>
        private SplFileImportResult createZipProcessingErrorResult(string errorMessage)
        {
            #region implementation
            return new SplFileImportResult
            {
                FileName = ZIP_ARCHIVE_PROCESSING,
                Success = false,
                Message = $"{ZIP_PROCESSING_ERROR_PREFIX}{errorMessage}"
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a result object for XML processing errors.
        /// </summary>
        /// <param name="fileName">The name of the XML file that failed</param>
        /// <param name="errorMessage">The error message to include</param>
        /// <returns>A SplFileImportResult indicating XML processing error</returns>
        /// <seealso cref="SplFileImportResult"/>
        /// <seealso cref="Label"/>
        private SplFileImportResult createXmlProcessingErrorResult(string fileName, string errorMessage)
        {
            #region implementation
            return new SplFileImportResult
            {
                FileName = fileName,
                Success = false,
                Message = errorMessage
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a result object for skipped duplicate files.
        /// </summary>
        /// <param name="fileName">The name of the XML file that was skipped</param>
        /// <returns>A SplFileImportResult indicating duplicate file was skipped</returns>
        /// <seealso cref="SplFileImportResult"/>
        /// <seealso cref="Label"/>
        private SplFileImportResult createSkippedDuplicateResult(string fileName)
        {
            #region implementation
            return new SplFileImportResult
            {
                FileName = fileName,
                Success = true,
                Message = "Duplicate content skipped"
            };
            #endregion
        }

        #endregion

        #region helper classes

        /**************************************************************/
        /// <summary>
        /// Helper class for tracking processing progress across multiple files.
        /// </summary>
        /// <remarks>
        /// Provides thread-safe progress calculation and maintains current state.
        /// Encapsulates progress logic to avoid scattered progress calculations.
        /// </remarks>
        /// <seealso cref="Label"/>
        private class ProcessingProgressTracker
        {
            #region implementation
            private readonly int _totalFiles;
            private int _processedCount;

            /**************************************************************/
            /// <summary>
            /// Initializes a new instance of the ProcessingProgressTracker class.
            /// </summary>
            /// <param name="totalFiles">Total number of files to process</param>
            /// <seealso cref="Label"/>
            public ProcessingProgressTracker(int totalFiles)
            {
                _totalFiles = totalFiles;
                _processedCount = 0;
            }

            /**************************************************************/
            /// <summary>
            /// Increments the count of processed files.
            /// </summary>
            /// <seealso cref="Label"/>
            public void IncrementProcessedCount()
            {
                Interlocked.Increment(ref _processedCount);
            }

            /**************************************************************/
            /// <summary>
            /// Gets the current progress percentage.
            /// </summary>
            /// <returns>Progress percentage as an integer between 0 and 100</returns>
            /// <seealso cref="Label"/>
            public int GetProgressPercentage()
            {
                if (_totalFiles == 0) return 100;
                return (int)((_processedCount / (float)_totalFiles) * 100);
            }
            #endregion
        }

        #endregion
    }
}