﻿using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Service;
using MedRecPro.DataAccess;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Service for managing SPL (Structured Product Labeling) data operations.
    /// Provides methods for creating, retrieving, and managing SPL XML data records
    /// using the repository pattern and avoiding duplicate entries.
    /// </summary>
    /// <remarks>
    /// This service encapsulates business logic for SPL data management and uses
    /// the generic Repository pattern for database operations. It includes
    /// duplicate prevention and user context management.
    /// </remarks>
    /// <seealso cref="SplData"/>
    /// <seealso cref="Repository{T}"/>
    /// <seealso cref="SplImportService"/>
    public class SplDataService
    {
        #region Private Fields

        private readonly Repository<SplData> _splDataRepository;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SplDataService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _encryptionKey;

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="SplDataService"/> class.
        /// </summary>
        /// <param name="splDataRepository">Repository for SPL data operations.</param>
        /// <param name="context">Database context for direct queries when needed.</param>
        /// <param name="logger">Logger for tracking operations and errors.</param>
        /// <param name="configuration">Configuration for accessing encryption settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when encryption key is missing from configuration.</exception>
        /// <remarks>
        /// All dependencies are injected through the constructor and should be registered
        /// in the dependency injection container.
        /// </remarks>
        /// <seealso cref="Repository{T}"/>
        /// <seealso cref="ApplicationDbContext"/>
        public SplDataService(
            Repository<SplData> splDataRepository,
            ApplicationDbContext context,
            ILogger<SplDataService> logger,
            IConfiguration configuration)
        {
            #region implementation
            _splDataRepository = splDataRepository ?? throw new ArgumentNullException(nameof(splDataRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _encryptionKey = _configuration["Security:DB:PKSecret"] ?? throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' is missing.");
            if (string.IsNullOrWhiteSpace(_encryptionKey))
            {
                throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' cannot be empty.");
            }
            #endregion
        }

        #endregion

        #region Public Methods

        /**************************************************************/
        /// <summary>
        /// Creates or retrieves an existing SPL data record based on XML content hash.
        /// This method prevents duplicate XML content from being stored multiple times.
        /// </summary>
        /// <param name="xmlContent">The SPL XML content to store or find.</param>
        /// <param name="userId">Optional user ID creating this record.</param>
        /// <returns>The encrypted ID of the created or existing SPL data record.</returns>
        /// <exception cref="ArgumentException">Thrown when xmlContent is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when database operations fail.</exception>
        /// <remarks>
        /// This method uses a hash of the XML content to check for duplicates before creating new records.
        /// If an identical XML document already exists, it returns the existing record's ID.
        /// </remarks>
        /// <example>
        /// <code>
        /// var encryptedId = await splDataService.GetOrCreateSplDataAsync(xmlContent, userId);
        /// </code>
        /// </example>
        /// <seealso cref="SplData"/>
        /// <seealso cref="CreateSplDataAsync"/>
        public async Task<string> GetOrCreateSplDataAsync(string xmlContent, Guid? splDataGuid = null, long? userId = null)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                throw new ArgumentException("XML content cannot be null or empty.", nameof(xmlContent));
            }

            try
            {
                // Generate hash of XML content for duplicate detection
                string contentHash = generateXmlContentHash(xmlContent);

                // Check if identical content already exists (not archived)
                var existingSplData = await findExistingSplDataByHashAsync(contentHash);

                if (existingSplData != null)
                {
                    _logger.LogInformation("Found existing SPL data record with ID {SplDataId} for content hash {ContentHash}",
                        existingSplData.SplDataID, contentHash);

                    // Return encrypted ID of existing record
                    return StringCipher.Encrypt(existingSplData.SplDataID.ToString(), _encryptionKey, StringCipher.EncryptionStrength.Fast);
                }

                // Create new record if no duplicate found
                return await CreateSplDataAsync(xmlContent, (Guid)splDataGuid, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrCreateSplDataAsync for user {UserId}", userId);
                throw;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new SPL data record with the specified XML content.
        /// </summary>
        /// <param name="xmlContent">The SPL XML content to store.</param>
        /// <param name="splDataGuid"></param>
        /// <param name="userId">Optional user ID creating this record.</param>
        /// <returns>The encrypted ID of the newly created SPL data record.</returns>
        /// <exception cref="ArgumentException">Thrown when xmlContent is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when record creation fails.</exception>
        /// <remarks>
        /// This method always creates a new record regardless of whether similar content exists.
        /// Use GetOrCreateSplDataAsync to avoid duplicates.
        /// </remarks>
        /// <example>
        /// <code>
        /// var encryptedId = await splDataService.CreateSplDataAsync(xmlContent, userId);
        /// </code>
        /// </example>
        /// <seealso cref="SplData"/>
        /// <seealso cref="GetOrCreateSplDataAsync"/>
        public async Task<string> CreateSplDataAsync(string xmlContent,Guid splDataGuid, long? userId = null)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                throw new ArgumentException("XML content cannot be null or empty.", nameof(xmlContent));
            }

            try
            {
                var splData = new SplData(xmlContent, splDataGuid, userId);

                _logger.LogInformation("Creating new SPL data record for user {UserId} with GUID {SplDataGuid}",
                    userId, splData.SplDataGUID);

                var encryptedId = await _splDataRepository.CreateAsync(splData);

                if (string.IsNullOrEmpty(encryptedId))
                {
                    throw new InvalidOperationException("Failed to create SPL data record - no ID returned.");
                }

                _logger.LogInformation("Successfully created SPL data record with encrypted ID {EncryptedId}", encryptedId);

                return encryptedId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating SPL data record for user {UserId}", userId);
                throw;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves an SPL data record by its encrypted ID.
        /// </summary>
        /// <param name="encryptedId">The encrypted ID of the SPL data record to retrieve.</param>
        /// <returns>The SPL data record if found; otherwise, null.</returns>
        /// <exception cref="ArgumentException">Thrown when encryptedId is null or empty.</exception>
        /// <remarks>
        /// This method uses the repository pattern to retrieve records and includes
        /// the encrypted ID in the returned object for API usage.
        /// </remarks>
        /// <example>
        /// <code>
        /// var splData = await splDataService.GetSplDataByIdAsync(encryptedId);
        /// </code>
        /// </example>
        /// <seealso cref="SplData"/>
        /// <seealso cref="Repository{T}.ReadByIdAsync"/>
        public async Task<SplData?> GetSplDataByIdAsync(string encryptedId)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(encryptedId))
            {
                throw new ArgumentException("Encrypted ID cannot be null or empty.", nameof(encryptedId));
            }

            try
            {
                var splData = await _splDataRepository.ReadByIdAsync(encryptedId);

                if (splData != null)
                {
                    // Set the encrypted ID for API usage
                    splData.EncryptedSplDataId = encryptedId;
                }

                return splData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving SPL data record with encrypted ID {EncryptedId}", encryptedId);
                throw;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Archives an SPL data record by setting its Archive flag to true.
        /// This provides soft deletion functionality.
        /// </summary>
        /// <param name="encryptedId">The encrypted ID of the SPL data record to archive.</param>
        /// <returns>True if the record was successfully archived; otherwise, false.</returns>
        /// <exception cref="ArgumentException">Thrown when encryptedId is null or empty.</exception>
        /// <remarks>
        /// Archived records are not deleted from the database but are marked as inactive.
        /// This preserves data integrity while removing records from active use.
        /// </remarks>
        /// <example>
        /// <code>
        /// bool archived = await splDataService.ArchiveSplDataAsync(encryptedId);
        /// </code>
        /// </example>
        /// <seealso cref="SplData.Archive"/>
        public async Task<bool> ArchiveSplDataAsync(string encryptedId)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(encryptedId))
            {
                throw new ArgumentException("Encrypted ID cannot be null or empty.", nameof(encryptedId));
            }

            try
            {
                var splData = await _splDataRepository.ReadByIdAsync(encryptedId);

                if (splData == null)
                {
                    _logger.LogWarning("SPL data record with encrypted ID {EncryptedId} not found for archiving", encryptedId);
                    return false;
                }

                splData.Archive = true;
                var updateResult = await _splDataRepository.UpdateAsync(splData);

                _logger.LogInformation("SPL data record with encrypted ID {EncryptedId} archived successfully", encryptedId);

                return updateResult > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving SPL data record with encrypted ID {EncryptedId}", encryptedId);
                throw;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves a paginated list of SPL data records, optionally including archived records.
        /// </summary>
        /// <param name="pageNumber">The page number to retrieve (1-based).</param>
        /// <param name="pageSize">The number of records per page.</param>
        /// <param name="includeArchived">Whether to include archived records in the results.</param>
        /// <returns>A collection of SPL data records for the specified page.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when pageNumber or pageSize are invalid.</exception>
        /// <remarks>
        /// This method provides efficient pagination for large datasets and allows
        /// filtering of archived records based on the includeArchived parameter.
        /// </remarks>
        /// <example>
        /// <code>
        /// var splDataList = await splDataService.GetSplDataListAsync(1, 50, false);
        /// </code>
        /// </example>
        /// <seealso cref="Repository{T}.ReadAllAsync"/>
        public async Task<IEnumerable<SplData>> GetSplDataListAsync(int? pageNumber = null, int? pageSize = null, bool includeArchived = false)
        {
            #region implementation
            try
            {
                var allRecords = await _splDataRepository.ReadAllAsync(pageNumber, pageSize);

                if (!includeArchived)
                {
                    // Filter out archived records if not requested
                    allRecords = allRecords.Where(sd => sd.Archive != true);
                }

                // Set encrypted IDs for API usage
                foreach (var splData in allRecords)
                {
                    splData.EncryptedSplDataId = StringCipher.Encrypt(splData.SplDataID.ToString(), _encryptionKey, StringCipher.EncryptionStrength.Fast);
                }

                return allRecords;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving SPL data list with pageNumber {PageNumber}, pageSize {PageSize}, includeArchived {IncludeArchived}",
                    pageNumber, pageSize, includeArchived);
                throw;
            }
            #endregion
        }

        #endregion

        #region Private Methods

        /**************************************************************/
        /// <summary>
        /// Generates a SHA256 hash of the XML content for duplicate detection.
        /// </summary>
        /// <param name="xmlContent">The XML content to hash.</param>
        /// <returns>A hexadecimal string representation of the hash.</returns>
        /// <remarks>
        /// This method normalizes whitespace before hashing to ensure consistent
        /// hash values for functionally identical XML content.
        /// </remarks>
        /// <seealso cref="GetOrCreateSplDataAsync"/>
        private string generateXmlContentHash(string xmlContent)
        {
            #region implementation
            // Normalize whitespace to ensure consistent hashing
            var normalizedContent = xmlContent.Trim()
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");

            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(normalizedContent));
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds an existing SPL data record by content hash to prevent duplicates.
        /// </summary>
        /// <param name="contentHash">The hash of the XML content to search for.</param>
        /// <returns>The existing SPL data record if found; otherwise, null.</returns>
        /// <remarks>
        /// This method performs a direct database query to check for existing content
        /// based on a computed hash. Only non-archived records are considered.
        /// Note: This is a simplified duplicate detection. In production, you might want
        /// to store the hash in a separate column for better performance.
        /// </remarks>
        /// <seealso cref="generateXmlContentHash"/>
        private async Task<SplData> findExistingSplDataByHashAsync(string contentHash)
        {
            #region implementation
            try
            {
                // Get the entity type for SplData using reflection
                var splDataType = typeof(SplData);

                // Use the non-generic Set method to get the DbSet
                var dbSet = _context.Set<SplData>();

                // TODO STORE HASH IN DATABASE FOR BETTER PERFORMANCE   

                // Get all records
                var allRecords = await _context.SplData
                    .Where(sd => sd.Archive != true)
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var record in allRecords)
                {

                    // Use reflection to get the SplXML property value
                    var splXmlProperty = splDataType.GetProperty("SplXML");
                    if (splXmlProperty != null)
                    {
                        var xmlContent = splXmlProperty.GetValue(record) as string;
                        if (!string.IsNullOrEmpty(xmlContent))
                        {
                            var recordHash = generateXmlContentHash(xmlContent);
                            if (recordHash.Equals(contentHash, StringComparison.OrdinalIgnoreCase))
                            {
                                return record;
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding existing SPL data by hash {ContentHash}", contentHash);
                throw;
            }
            #endregion
        }

        #endregion
    }
}
