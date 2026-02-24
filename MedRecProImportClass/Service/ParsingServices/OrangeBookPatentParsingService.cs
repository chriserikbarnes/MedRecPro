using MedRecProImportClass.Data;
using MedRecProImportClass.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace MedRecProImportClass.Service.ParsingServices
{
    /*************************************************************/
    /// <summary>
    /// Parses the FDA Orange Book patent.txt flat file and upserts data into the
    /// <see cref="OrangeBook.Patent"/> table. Each patent record is linked to its parent
    /// <see cref="OrangeBook.Product"/> via the composite natural key (ApplType, ApplNo, ProductNo).
    /// The import is idempotent: existing records are updated and new records are inserted,
    /// allowing the calling console app to fail and resume without data loss or duplication.
    /// </summary>
    /// <remarks>
    /// The patent.txt file uses tilde (~) as the field delimiter with a header row containing
    /// 10 columns. The service runs on its own processing thread via a scoped
    /// <see cref="ApplicationDbContext"/> created from <see cref="IServiceScopeFactory"/>.
    /// Patents are processed in batches of 5,000 to manage memory. The upsert natural key
    /// is (ApplType, ApplNo, ProductNo, PatentNo) — uniquely identifying a patent per product.
    ///
    /// Boolean flag columns (Drug_Substance_Flag, Drug_Product_Flag, Delist_Flag) use "Y"
    /// for true and blank/empty for false. Date columns use "MMM d, yyyy" format
    /// (e.g., "Aug 24, 2026").
    /// </remarks>
    /// <seealso cref="OrangeBook"/>
    /// <seealso cref="OrangeBook.Patent"/>
    /// <seealso cref="OrangeBook.Product"/>
    /// <seealso cref="OrangeBookProductParsingService"/>
    /// <seealso cref="OrangeBookImportResult"/>
    public class OrangeBookPatentParsingService
    {
        #region private fields

        /*************************************************************/
        /// <summary>
        /// Factory for creating dependency injection scopes on the processing thread.
        /// </summary>
        /// <seealso cref="IServiceScopeFactory"/>
        private readonly IServiceScopeFactory _scopeFactory;

        /*************************************************************/
        /// <summary>
        /// Logger for tracking import operations and reporting errors.
        /// </summary>
        /// <seealso cref="ILogger{OrangeBookPatentParsingService}"/>
        private readonly ILogger<OrangeBookPatentParsingService> _logger;

        #endregion

        #region constants

        private const char FIELD_DELIMITER = '~';
        private const int EXPECTED_COLUMN_COUNT = 10;
        private const int PATENT_BATCH_SIZE = 5000;

        // Column indices (zero-based) for the tilde-delimited patent.txt
        private const int COL_APPL_TYPE = 0;
        private const int COL_APPL_NO = 1;
        private const int COL_PRODUCT_NO = 2;
        private const int COL_PATENT_NO = 3;
        private const int COL_PATENT_EXPIRE_DATE = 4;
        private const int COL_DRUG_SUBSTANCE_FLAG = 5;
        private const int COL_DRUG_PRODUCT_FLAG = 6;
        private const int COL_PATENT_USE_CODE = 7;
        private const int COL_DELIST_FLAG = 8;
        private const int COL_SUBMISSION_DATE = 9;

        #endregion

        /*************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="OrangeBookPatentParsingService"/>
        /// with the required dependency injection services.
        /// </summary>
        /// <param name="scopeFactory">Factory for creating scoped service providers on the processing thread.</param>
        /// <param name="logger">Logger for tracking import operations and errors.</param>
        /// <example>
        /// <code>
        /// // Typically registered via DI:
        /// services.AddScoped&lt;OrangeBookPatentParsingService&gt;();
        /// </code>
        /// </example>
        /// <seealso cref="IServiceScopeFactory"/>
        /// <seealso cref="ILogger{OrangeBookPatentParsingService}"/>
        public OrangeBookPatentParsingService(
            IServiceScopeFactory scopeFactory,
            ILogger<OrangeBookPatentParsingService> logger)
        {
            #region implementation
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Parses the contents of patent.txt and upserts all <see cref="OrangeBook.Patent"/>
        /// records. Each patent is linked to its parent product via the composite natural key
        /// (ApplType, ApplNo, ProductNo). Existing records are updated; new records are inserted.
        /// The operation is idempotent and safe to re-run after a failure.
        /// </summary>
        /// <param name="fileContent">The full text content of patent.txt (tilde-delimited with header row).</param>
        /// <param name="result">
        /// The shared <see cref="OrangeBookImportResult"/> from the product import phase.
        /// Patent-specific fields (PatentsCreated, PatentsUpdated, etc.) are populated by this method.
        /// </param>
        /// <param name="token">Cancellation token for cooperative cancellation.</param>
        /// <param name="reportProgress">Optional delegate for progress reporting.</param>
        /// <returns>The same <see cref="OrangeBookImportResult"/> with patent fields populated.</returns>
        /// <example>
        /// <code>
        /// var service = scope.ServiceProvider.GetRequiredService&lt;OrangeBookPatentParsingService&gt;();
        /// var result = await service.ProcessPatentsFileAsync(patentText, importResult, cancellationToken);
        /// Console.WriteLine($"Created {result.PatentsCreated}, Updated {result.PatentsUpdated}");
        /// </code>
        /// </example>
        /// <remarks>
        /// Creates its own <see cref="IServiceScope"/> and <see cref="ApplicationDbContext"/>
        /// for thread safety. The database connection is manually opened and held open for
        /// the duration of the import to avoid repeated open/close overhead (following the
        /// pattern in <see cref="OrangeBookProductParsingService"/>).
        ///
        /// Products must be imported first so that the product lookup can resolve
        /// OrangeBookProductID for each patent row.
        /// </remarks>
        /// <seealso cref="OrangeBookImportResult"/>
        /// <seealso cref="OrangeBook.Patent"/>
        /// <seealso cref="OrangeBook.Product"/>
        public async Task<OrangeBookImportResult> ProcessPatentsFileAsync(
            string fileContent,
            OrangeBookImportResult result,
            CancellationToken token,
            Action<string>? reportProgress = null)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(fileContent))
            {
                result.Success = false;
                result.Errors.Add("Patent file content is null or empty.");
                return result;
            }

            // Create a dedicated scope for this import thread
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Manually open connection and keep it open for the entire import
            // to avoid repeated open/close overhead per SaveChangesAsync call
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync(token);

            try
            {
                // Step 1: Parse the flat file into row arrays
                reportProgress?.Invoke("Parsing patent.txt lines...");
                var dataRows = parseLines(fileContent, result);
                _logger.LogInformation("Parsed {RowCount} data rows from patent.txt ({Skipped} malformed rows skipped)",
                    dataRows.Count, result.MalformedRowsSkipped);

                if (dataRows.Count == 0)
                {
                    result.Success = false;
                    result.Errors.Add("No valid data rows found in patent.txt.");
                    return result;
                }

                token.ThrowIfCancellationRequested();

                // Step 2: Load product lookup for FK resolution
                reportProgress?.Invoke("Loading product lookup for patent linking...");
                var productLookup = await loadProductLookupAsync(context, token);
                _logger.LogInformation("Loaded {Count} products for patent→product linking", productLookup.Count);

                token.ThrowIfCancellationRequested();

                // Step 3: Upsert patents in batches
                reportProgress?.Invoke("Upserting patents...");
                await upsertPatentsAsync(dataRows, productLookup, context, result, reportProgress, token);
                _logger.LogInformation("Patents: {Created} created, {Updated} updated, {Linked} linked to products, {Unlinked} unlinked",
                    result.PatentsCreated, result.PatentsUpdated, result.PatentsLinkedToProduct, result.UnlinkedPatents);

                // Final status
                result.Success = result.Errors.Count == 0;
                var patentMessage = $"{result.PatentsCreated + result.PatentsUpdated} patents processed, " +
                                   $"{result.PatentsLinkedToProduct} linked to products.";
                result.Message = result.Message != null
                    ? $"{result.Message} {patentMessage}"
                    : patentMessage;

                reportProgress?.Invoke(patentMessage);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Orange Book patent import was cancelled.");
                result.Success = false;
                result.Errors.Add("Patent import was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during Orange Book patent import.");
                result.Success = false;
                result.Errors.Add($"Patent import critical error: {ex.Message}");
            }
            finally
            {
                await connection.CloseAsync();
            }

            return result;

            #endregion
        }

        #region line parsing

        /*************************************************************/
        /// <summary>
        /// Splits the file content into validated row arrays, skipping the header row
        /// and any malformed rows that do not have the expected column count.
        /// </summary>
        /// <param name="fileContent">Raw text content of patent.txt.</param>
        /// <param name="result">Import result to track malformed row count.</param>
        /// <returns>A list of string arrays, each representing one valid data row.</returns>
        /// <seealso cref="OrangeBookImportResult"/>
        private List<string[]> parseLines(string fileContent, OrangeBookImportResult result)
        {
            #region implementation
            var dataRows = new List<string[]>();
            var lines = fileContent.Split('\n');

            // Skip the header row (index 0) and process data rows
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');

                // Skip empty lines at end of file
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fields = line.Split(FIELD_DELIMITER);

                if (fields.Length != EXPECTED_COLUMN_COUNT)
                {
                    result.MalformedRowsSkipped++;
                    _logger.LogWarning("Skipping malformed patent row {LineNumber}: expected {Expected} columns, got {Actual}",
                        i + 1, EXPECTED_COLUMN_COUNT, fields.Length);
                    continue;
                }

                dataRows.Add(fields);
            }

            return dataRows;
            #endregion
        }

        #endregion

        #region field parsing helpers

        /*************************************************************/
        /// <summary>
        /// Parses a date string in "MMM d, yyyy" format (e.g., "Aug 24, 2026", "Feb 1, 2027")
        /// into a <see cref="DateTime"/>. Returns null for blank, empty, or unparseable values.
        /// </summary>
        /// <param name="dateText">The raw date string from patent.txt.</param>
        /// <returns>The parsed date, or null if the text is empty or unparseable.</returns>
        /// <seealso cref="OrangeBook.Patent.PatentExpireDate"/>
        /// <seealso cref="OrangeBook.Patent.SubmissionDate"/>
        private DateTime? parseDate(string dateText)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(dateText))
                return null;

            var trimmed = dateText.Trim();

            // Dates appear as "MMM d, yyyy" or "MMM dd, yyyy" (e.g., "Aug 24, 2026", "Feb 1, 2027")
            string[] formats = { "MMM dd, yyyy", "MMM d, yyyy" };

            if (DateTime.TryParseExact(trimmed, formats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }

            _logger.LogWarning("Could not parse patent date: '{DateText}'", trimmed);
            return null;

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Converts a "Y"/blank flag value to boolean. Returns true only when the value
        /// is "Y" (case-insensitive); blank, null, or any other value returns false.
        /// </summary>
        /// <param name="value">The raw flag value from the Drug_Substance_Flag, Drug_Product_Flag, or Delist_Flag column.</param>
        /// <returns>True if the value is "Y"; false otherwise.</returns>
        /// <seealso cref="OrangeBook.Patent.DrugSubstanceFlag"/>
        /// <seealso cref="OrangeBook.Patent.DrugProductFlag"/>
        /// <seealso cref="OrangeBook.Patent.DelistFlag"/>
        private bool parseYFlag(string value)
        {
            #region implementation
            return !string.IsNullOrWhiteSpace(value)
                && value.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase);
            #endregion
        }

        #endregion

        #region product lookup

        /*************************************************************/
        /// <summary>
        /// Loads all <see cref="OrangeBook.Product"/> records into a dictionary keyed by
        /// the composite natural key (ApplType, ApplNo, ProductNo), mapping to OrangeBookProductID.
        /// Used for resolving the foreign key when importing patent records.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary mapping (ApplType, ApplNo, ProductNo) to OrangeBookProductID.</returns>
        /// <seealso cref="OrangeBook.Product"/>
        /// <seealso cref="OrangeBook.Patent.OrangeBookProductID"/>
        private async Task<Dictionary<(string, string, string), int>> loadProductLookupAsync(
            ApplicationDbContext context,
            CancellationToken token)
        {
            #region implementation

            var products = await context.Set<OrangeBook.Product>()
                .Where(p => p.OrangeBookProductID != null)
                .Select(p => new
                {
                    ApplType = p.ApplType ?? string.Empty,
                    ApplNo = p.ApplNo ?? string.Empty,
                    ProductNo = p.ProductNo ?? string.Empty,
                    ProductID = p.OrangeBookProductID!.Value
                })
                .ToListAsync(token);

            var lookup = new Dictionary<(string, string, string), int>();
            foreach (var p in products)
            {
                lookup[(p.ApplType, p.ApplNo, p.ProductNo)] = p.ProductID;
            }

            return lookup;

            #endregion
        }

        #endregion

        #region patent upsert

        /*************************************************************/
        /// <summary>
        /// Upserts patent records in batches, creating new <see cref="OrangeBook.Patent"/>
        /// entities or updating existing ones. The upsert natural key is
        /// (ApplType, ApplNo, ProductNo, PatentNo). Each patent is linked to its parent
        /// product via the <paramref name="productLookup"/> dictionary.
        /// </summary>
        /// <param name="dataRows">Validated row arrays from <see cref="parseLines"/>.</param>
        /// <param name="productLookup">Dictionary mapping product natural key to OrangeBookProductID.</param>
        /// <param name="context">The database context for the current import scope.</param>
        /// <param name="result">Import result to track patent counts.</param>
        /// <param name="reportProgress">Optional progress reporting callback.</param>
        /// <param name="token">Cancellation token.</param>
        /// <seealso cref="OrangeBook.Patent"/>
        /// <seealso cref="OrangeBook.Product"/>
        private async Task upsertPatentsAsync(
            List<string[]> dataRows,
            Dictionary<(string, string, string), int> productLookup,
            ApplicationDbContext context,
            OrangeBookImportResult result,
            Action<string>? reportProgress,
            CancellationToken token)
        {
            #region implementation

            // Load all existing patents into a lookup by natural key (ApplType, ApplNo, ProductNo, PatentNo)
            var existingPatents = await context.Set<OrangeBook.Patent>()
                .ToDictionaryAsync(
                    p => (p.ApplType ?? string.Empty, p.ApplNo ?? string.Empty,
                          p.ProductNo ?? string.Empty, p.PatentNo ?? string.Empty),
                    p => p,
                    token);

            // Track unlinked product keys to avoid repeated warnings for the same key
            var warnedProductKeys = new HashSet<(string, string, string)>();

            // Process in batches
            int totalRows = dataRows.Count;
            int batchNumber = 0;

            for (int offset = 0; offset < totalRows; offset += PATENT_BATCH_SIZE)
            {
                token.ThrowIfCancellationRequested();

                batchNumber++;
                int batchEnd = Math.Min(offset + PATENT_BATCH_SIZE, totalRows);
                int batchCreated = 0;
                int batchUpdated = 0;

                for (int i = offset; i < batchEnd; i++)
                {
                    var row = dataRows[i];
                    var applType = row[COL_APPL_TYPE]?.Trim() ?? string.Empty;
                    var applNo = row[COL_APPL_NO]?.Trim() ?? string.Empty;
                    var productNo = row[COL_PRODUCT_NO]?.Trim() ?? string.Empty;
                    var patentNo = row[COL_PATENT_NO]?.Trim() ?? string.Empty;
                    var naturalKey = (applType, applNo, productNo, patentNo);

                    // Resolve OrangeBookProductID via product lookup
                    var productKey = (applType, applNo, productNo);
                    int? productId = null;
                    if (productLookup.TryGetValue(productKey, out var pid))
                    {
                        productId = pid;
                    }
                    else if (warnedProductKeys.Add(productKey))
                    {
                        // Only warn once per unique product key
                        _logger.LogWarning("No matching product for patent row: ApplType={ApplType}, ApplNo={ApplNo}, ProductNo={ProductNo}",
                            applType, applNo, productNo);
                    }

                    // Parse field values
                    var patentExpireDate = parseDate(row[COL_PATENT_EXPIRE_DATE]);
                    var drugSubstanceFlag = parseYFlag(row[COL_DRUG_SUBSTANCE_FLAG]);
                    var drugProductFlag = parseYFlag(row[COL_DRUG_PRODUCT_FLAG]);
                    var patentUseCode = row[COL_PATENT_USE_CODE]?.Trim();
                    var delistFlag = parseYFlag(row[COL_DELIST_FLAG]);
                    var submissionDate = parseDate(row[COL_SUBMISSION_DATE]);

                    if (existingPatents.TryGetValue(naturalKey, out var existing))
                    {
                        // Update all fields on the tracked entity
                        existing.OrangeBookProductID = productId;
                        existing.PatentExpireDate = patentExpireDate;
                        existing.DrugSubstanceFlag = drugSubstanceFlag;
                        existing.DrugProductFlag = drugProductFlag;
                        existing.PatentUseCode = string.IsNullOrWhiteSpace(patentUseCode) ? null : patentUseCode;
                        existing.DelistFlag = delistFlag;
                        existing.SubmissionDate = submissionDate;
                        batchUpdated++;

                        if (productId.HasValue)
                            result.PatentsLinkedToProduct++;
                        else
                            result.UnlinkedPatents++;
                    }
                    else
                    {
                        // Insert new patent
                        var newPatent = new OrangeBook.Patent
                        {
                            ApplType = applType,
                            ApplNo = applNo,
                            ProductNo = productNo,
                            PatentNo = patentNo,
                            OrangeBookProductID = productId,
                            PatentExpireDate = patentExpireDate,
                            DrugSubstanceFlag = drugSubstanceFlag,
                            DrugProductFlag = drugProductFlag,
                            PatentUseCode = string.IsNullOrWhiteSpace(patentUseCode) ? null : patentUseCode,
                            DelistFlag = delistFlag,
                            SubmissionDate = submissionDate
                        };

                        context.Set<OrangeBook.Patent>().Add(newPatent);

                        // Track for the lookup so subsequent batches know this key exists
                        existingPatents[naturalKey] = newPatent;
                        batchCreated++;

                        if (productId.HasValue)
                            result.PatentsLinkedToProduct++;
                        else
                            result.UnlinkedPatents++;
                    }
                }

                // Save the batch
                await context.SaveChangesAsync(token);

                result.PatentsCreated += batchCreated;
                result.PatentsUpdated += batchUpdated;

                reportProgress?.Invoke($"Patents batch {batchNumber}: {batchCreated} created, {batchUpdated} updated " +
                    $"({Math.Min(batchEnd, totalRows)}/{totalRows} rows processed)");

                // Clear change tracker to free memory between batches
                context.ChangeTracker.Clear();

                // Re-load existing patents for the next batch since tracker was cleared
                if (batchEnd < totalRows)
                {
                    existingPatents = await context.Set<OrangeBook.Patent>()
                        .ToDictionaryAsync(
                            p => (p.ApplType ?? string.Empty, p.ApplNo ?? string.Empty,
                                  p.ProductNo ?? string.Empty, p.PatentNo ?? string.Empty),
                            p => p,
                            token);
                }
            }

            #endregion
        }

        #endregion
    }
}
