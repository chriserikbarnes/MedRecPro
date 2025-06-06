using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MedRecPro.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MedRecPro.Services
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
        /// Service provider for dependency injection and service resolution.
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Logger instance for tracking import operations and debugging issues.
        /// </summary>
        private readonly ILogger<SplImportService> _logger;

        /// <summary>
        /// XML parser service responsible for parsing SPL XML content and saving data to the database.
        /// </summary>
        private readonly SplXmlParser _xmlParser; // If SplXmlParser is registered as a service
                                                  // Or: private readonly SplXmlParser _xmlParser = new SplXmlParser(...); if not DI
        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SplImportService with required dependencies.
        /// </summary>
        /// <param name="serviceProvider">Service provider for dependency injection</param>
        /// <param name="logger">Logger for tracking operations and errors</param>
        /// <param name="xmlParser">XML parser service for processing SPL data</param>
        /// <example>
        /// // Typically injected via dependency injection container
        /// services.AddScoped&lt;SplImportService&gt;();
        /// </example>
        public SplImportService(IServiceProvider serviceProvider, ILogger<SplImportService> logger, SplXmlParser xmlParser)
        {
            #region implementation
            // Initialize all required dependencies for the service
            _serviceProvider = serviceProvider;
            _logger = logger;
            _xmlParser = xmlParser; // Assumes SplXmlParser is registered
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes multiple ZIP files containing SPL XML data asynchronously.
        /// Each ZIP file is extracted and its XML entries are parsed and saved to the database.
        /// </summary>
        /// <param name="zipFiles">Collection of uploaded ZIP files to process</param>
        /// <returns>A list of SplZipImportResult objects containing the results for each ZIP file processed</returns>
        /// <example>
        /// var zipFiles = Request.Form.Files.Where(f => f.FileName.EndsWith(".zip")).ToList();
        /// var results = await splImportService.ProcessZipFilesAsync(zipFiles);
        /// foreach(var result in results)
        /// {
        ///     Console.WriteLine($"Processed {result.ZipFileName}: {result.FileResults.Count} files");
        /// }
        /// </example>
        /// <remarks>
        /// This method handles errors gracefully and continues processing remaining files even if individual files fail.
        /// Empty ZIP files and non-XML entries are logged but do not stop the overall process.
        /// </remarks>
        public async Task<List<SplZipImportResult>> ProcessZipFilesAsync(List<IFormFile> zipFiles)
        {
            #region implementation
            // Initialize collection to store results from all ZIP files
            var allZipResults = new List<SplZipImportResult>();

            // Process each ZIP file individually to ensure one failure doesn't affect others
            foreach (var zipFile in zipFiles)
            {
                // Create result object for this specific ZIP file
                var zipResult = new SplZipImportResult { ZipFileName = zipFile.FileName };

                allZipResults.Add(zipResult);

                _logger.LogInformation("Processing ZIP file: {ZipFileName}", zipFile.FileName);

                // Check if ZIP file has content before attempting to process
                if (zipFile.Length == 0)
                {
                    _logger.LogWarning("ZIP file {ZipFileName} is empty.", zipFile.FileName);
                    // Add a file result indicating empty zip or handle as needed
                    var emptyFileResult = new SplFileImportResult { FileName = "ZIP Archive", Success = false, Message = "ZIP file is empty or invalid." };
                    zipResult.FileResults.Add(emptyFileResult);
                    continue; // Skip to next ZIP file
                }

                try
                {
                    // Open and read the ZIP archive contents
                    using var stream = zipFile.OpenReadStream();

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
                            using (var entryStream = entry.Open())
                            using (var reader = new StreamReader(entryStream, Encoding.UTF8)) // Assuming UTF-8
                            {
                                xmlContent = await reader.ReadToEndAsync();
                            }

                            // Parse the XML content and save to database
                            var fileImportResult = await _xmlParser.ParseAndSaveSplDataAsync(xmlContent, entry.FullName);

                            zipResult.FileResults.Add(fileImportResult);
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