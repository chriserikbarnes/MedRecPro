using MedRecPro.Models;
using Microsoft.AspNetCore.Http;
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
            long? currentUserId,  // Add this parameter
            CancellationToken token,
            Action<int> fileCounter,
            Action<string>? updateStatus = null,
            Action<List<SplZipImportResult>>? results = null)
        {
            #region implementation
            Guid xmlFileGuid;

            // Initialize collection to store results from all ZIP files
            var allZipResults = new List<SplZipImportResult>();

            // Counter to track progress percentage across all files
            int count = 0;

            // Process each ZIP file individually to ensure one failure doesn't affect others
            foreach (var zipFile in bufferedFiles)
            {
                // Create result object for this specific ZIP file
                var zipResult = new SplZipImportResult { ZipFileName = zipFile.FileName };

                allZipResults.Add(zipResult);

                _logger.LogInformation("Processing ZIP file: {ZipFileName}", zipFile.FileName);

                try
                {
                    // Open and read the ZIP archive contents from temporary file path
                    using var stream = new FileStream(zipFile.TempFilePath, FileMode.Open, FileAccess.Read);

                    // Check if ZIP file has content before attempting to process
                    if (stream.Length == 0)
                    {
                        _logger.LogWarning("ZIP file {ZipFileName} is empty.", zipFile.FileName);

                        // Add a file result indicating empty zip or handle as needed
                        var emptyFileResult = new SplFileImportResult { FileName = "ZIP Archive", Success = false, Message = "ZIP file is empty or invalid." };

                        zipResult.FileResults.Add(emptyFileResult);
                        continue; // Skip to next ZIP file
                    }

                    // Open the ZIP archive for reading entries
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

                    // Process each entry in the ZIP archive
                    foreach (var entry in archive.Entries)
                    {
                        // Only process XML files that have content
                        if (entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && entry.Length > 0)
                        {
                            _logger.LogInformation("Processing XML entry: {XmlFileName} from {ZipFileName}", entry.FullName, zipFile.FileName);

                            // Extract XML content from the archive entry
                            string xmlContent;

                            // Read the XML content using UTF-8 encoding
                            using (var entryStream = entry.Open())
                            using (var reader = new StreamReader(entryStream, Encoding.UTF8)) // Assuming UTF-8
                            {
                                xmlContent = await reader.ReadToEndAsync();
                                Guid.TryParse(entry.Name.Split(".").FirstOrDefault(), out xmlFileGuid);
                            }

                            // Create a new scope for dependency injection to ensure proper resource management
                            using (var scope = _scopeFactory.CreateScope())
                            {

                                //Store raw XML content in SplData table
                                var splDataService = scope.ServiceProvider
                                  .GetRequiredService<SplDataService>();

                                // Get the XML parser service from the scoped provider
                                var xmlParser = scope.ServiceProvider
                                    .GetRequiredService<SplXmlParser>();

                                // Don't process dupes
                                if (await splDataService.IsDuplicateSplDataAsync(xmlContent, xmlFileGuid)) continue;

                                try
                                {
                                    // Store or find existing XML content
                                    var encryptedSplDataId = await splDataService
                                        .GetOrCreateSplDataAsync(xmlContent, xmlFileGuid, currentUserId);

                                    _logger.LogInformation("Stored XML content with encrypted SplData ID: {EncryptedSplDataId} for file {XmlFileName}",
                                        encryptedSplDataId, entry.FullName);

                                    updateStatus?.Invoke($"Stored XML content for {entry.FullName}");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Failed to store XML content for file {XmlFileName}", entry.FullName);

                                    // Add error result but continue processing
                                    var errorResult = new SplFileImportResult
                                    {
                                        FileName = entry.FullName,
                                        Success = false,
                                        Message = $"Failed to store XML content: {ex.Message}"
                                    };
                                    zipResult.FileResults.Add(errorResult);
                                    continue;
                                }

                                // Parse and save the SPL data from the XML content
                                var fileImportResult = await xmlParser
                                    .ParseAndSaveSplDataAsync(xmlContent,
                                    entry.FullName,
                                    updateStatus);

                                // Add the result to the ZIP result collection
                                zipResult.FileResults.Add(fileImportResult);

                                // Invoke results callback if provided to update calling code
                                results?.Invoke(allZipResults.ToList());
                            }

                            count++;

                            // Report progress to the caller based on files processed
                            int pct = (int)((count / (float)bufferedFiles.Count) * 100);
                            fileCounter(pct);
                        }
                        else
                        {
                            // Log skipped entries for debugging purposes
                            _logger.LogDebug("Skipping non-XML or empty entry: {EntryFullName} in {ZipFileName}", entry.FullName, zipFile.FileName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Handle any errors during ZIP processing and continue with next file
                    _logger.LogError(ex, "Error processing ZIP file {ZipFileName}", zipFile.FileName);
                    var errorFileResult = new SplFileImportResult { FileName = "ZIP Archive Processing", Success = false, Message = $"Error processing ZIP: {ex.Message}" };

                    zipResult.FileResults.Add(errorFileResult);
                }
            }

            // Return all results for review by calling code
            return allZipResults;
            #endregion
        }
    }
}