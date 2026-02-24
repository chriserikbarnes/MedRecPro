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
    /// Parses the FDA Orange Book exclusivity.txt flat file and upserts data into the
    /// <see cref="OrangeBook.Exclusivity"/> table. Each exclusivity record is linked to its
    /// parent <see cref="OrangeBook.Product"/> via the composite natural key
    /// (ApplType, ApplNo, ProductNo). The import is idempotent: existing records are updated
    /// and new records are inserted, allowing the calling console app to fail and resume
    /// without data loss or duplication.
    /// </summary>
    /// <remarks>
    /// The exclusivity.txt file uses tilde (~) as the field delimiter with a header row
    /// containing 5 columns:
    /// <list type="bullet">
    /// <item><description>Appl_Type — Application type ("N" for NDA, "A" for ANDA)</description></item>
    /// <item><description>Appl_No — Zero-padded application number (e.g., "017031")</description></item>
    /// <item><description>Product_No — Product number within the application (e.g., "001")</description></item>
    /// <item><description>Exclusivity_Code — Exclusivity type code (e.g., "NCE", "ODE-417", "RTO", "M-14")</description></item>
    /// <item><description>Exclusivity_Date — Expiration date in "MMM d, yyyy" format</description></item>
    /// </list>
    ///
    /// The service runs on its own processing thread via a scoped
    /// <see cref="ApplicationDbContext"/> created from <see cref="IServiceScopeFactory"/>.
    /// Exclusivity records are processed in batches of 5,000 to manage memory. The upsert
    /// natural key is (ApplType, ApplNo, ProductNo, ExclusivityCode) — uniquely identifying
    /// an exclusivity grant per product.
    ///
    /// One product can have multiple exclusivity records with different codes (e.g., product
    /// N/021825/001 may have ODE-417, ODE-420, and ODE-421 simultaneously).
    ///
    /// Products must be imported first so that the product lookup can resolve
    /// OrangeBookProductID for each exclusivity row.
    /// </remarks>
    /// <seealso cref="OrangeBook"/>
    /// <seealso cref="OrangeBook.Exclusivity"/>
    /// <seealso cref="OrangeBook.Product"/>
    /// <seealso cref="OrangeBookProductParsingService"/>
    /// <seealso cref="OrangeBookPatentParsingService"/>
    /// <seealso cref="OrangeBookImportResult"/>
    public class OrangeBookExclusivityParsingService
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
        /// <seealso cref="ILogger{OrangeBookExclusivityParsingService}"/>
        private readonly ILogger<OrangeBookExclusivityParsingService> _logger;

        #endregion

        #region constants

        private const char FIELD_DELIMITER = '~';
        private const int EXPECTED_COLUMN_COUNT = 5;
        private const int EXCLUSIVITY_BATCH_SIZE = 5000;

        // Column indices (zero-based) for the tilde-delimited exclusivity.txt
        private const int COL_APPL_TYPE = 0;
        private const int COL_APPL_NO = 1;
        private const int COL_PRODUCT_NO = 2;
        private const int COL_EXCLUSIVITY_CODE = 3;
        private const int COL_EXCLUSIVITY_DATE = 4;

        #endregion

        /*************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="OrangeBookExclusivityParsingService"/>
        /// with the required dependency injection services.
        /// </summary>
        /// <param name="scopeFactory">Factory for creating scoped service providers on the processing thread.</param>
        /// <param name="logger">Logger for tracking import operations and errors.</param>
        /// <example>
        /// <code>
        /// // Typically registered via DI:
        /// services.AddScoped&lt;OrangeBookExclusivityParsingService&gt;();
        /// </code>
        /// </example>
        /// <seealso cref="IServiceScopeFactory"/>
        /// <seealso cref="ILogger{OrangeBookExclusivityParsingService}"/>
        public OrangeBookExclusivityParsingService(
            IServiceScopeFactory scopeFactory,
            ILogger<OrangeBookExclusivityParsingService> logger)
        {
            #region implementation
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Parses the contents of exclusivity.txt and upserts all <see cref="OrangeBook.Exclusivity"/>
        /// records. Each exclusivity record is linked to its parent product via the composite
        /// natural key (ApplType, ApplNo, ProductNo). Existing records are updated; new records
        /// are inserted. The operation is idempotent and safe to re-run after a failure.
        /// </summary>
        /// <param name="fileContent">The full text content of exclusivity.txt (tilde-delimited with header row).</param>
        /// <param name="result">
        /// The shared <see cref="OrangeBookImportResult"/> from the product/patent import phases.
        /// Exclusivity-specific fields (ExclusivityCreated, ExclusivityUpdated, etc.) are populated
        /// by this method.
        /// </param>
        /// <param name="token">Cancellation token for cooperative cancellation.</param>
        /// <param name="reportProgress">Optional delegate for progress reporting.</param>
        /// <returns>The same <see cref="OrangeBookImportResult"/> with exclusivity fields populated.</returns>
        /// <example>
        /// <code>
        /// var service = scope.ServiceProvider.GetRequiredService&lt;OrangeBookExclusivityParsingService&gt;();
        /// var result = await service.ProcessExclusivityFileAsync(exclusivityText, importResult, cancellationToken);
        /// Console.WriteLine($"Created {result.ExclusivityCreated}, Updated {result.ExclusivityUpdated}");
        /// </code>
        /// </example>
        /// <remarks>
        /// Creates its own <see cref="IServiceScope"/> and <see cref="ApplicationDbContext"/>
        /// for thread safety. The database connection is manually opened and held open for
        /// the duration of the import to avoid repeated open/close overhead (following the
        /// pattern in <see cref="OrangeBookPatentParsingService"/>).
        ///
        /// Products must be imported first so that the product lookup can resolve
        /// OrangeBookProductID for each exclusivity row.
        /// </remarks>
        /// <seealso cref="OrangeBookImportResult"/>
        /// <seealso cref="OrangeBook.Exclusivity"/>
        /// <seealso cref="OrangeBook.Product"/>
        public async Task<OrangeBookImportResult> ProcessExclusivityFileAsync(
            string fileContent,
            OrangeBookImportResult result,
            CancellationToken token,
            Action<string>? reportProgress = null)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(fileContent))
            {
                result.Success = false;
                result.Errors.Add("Exclusivity file content is null or empty.");
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
                reportProgress?.Invoke("Parsing exclusivity.txt lines...");
                var dataRows = parseLines(fileContent, result);
                _logger.LogInformation("Parsed {RowCount} data rows from exclusivity.txt ({Skipped} malformed rows skipped)",
                    dataRows.Count, result.MalformedRowsSkipped);

                if (dataRows.Count == 0)
                {
                    result.Success = false;
                    result.Errors.Add("No valid data rows found in exclusivity.txt.");
                    return result;
                }

                token.ThrowIfCancellationRequested();

                // Step 2: Load product lookup for FK resolution
                reportProgress?.Invoke("Loading product lookup for exclusivity linking...");
                var productLookup = await loadProductLookupAsync(context, token);
                _logger.LogInformation("Loaded {Count} products for exclusivity→product linking", productLookup.Count);

                token.ThrowIfCancellationRequested();

                // Step 3: Upsert exclusivity records in batches
                reportProgress?.Invoke("Upserting exclusivity records...");
                await upsertExclusivityAsync(dataRows, productLookup, context, result, reportProgress, token);
                _logger.LogInformation("Exclusivity: {Created} created, {Updated} updated, {Linked} linked to products, {Unlinked} unlinked",
                    result.ExclusivityCreated, result.ExclusivityUpdated, result.ExclusivityLinkedToProduct, result.UnlinkedExclusivity);

                // Final status
                result.Success = result.Errors.Count == 0;
                var exclusivityMessage = $"{result.ExclusivityCreated + result.ExclusivityUpdated} exclusivity records processed, " +
                                         $"{result.ExclusivityLinkedToProduct} linked to products.";
                result.Message = result.Message != null
                    ? $"{result.Message} {exclusivityMessage}"
                    : exclusivityMessage;

                reportProgress?.Invoke(exclusivityMessage);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Orange Book exclusivity import was cancelled.");
                result.Success = false;
                result.Errors.Add("Exclusivity import was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during Orange Book exclusivity import.");
                result.Success = false;
                result.Errors.Add($"Exclusivity import critical error: {ex.Message}");
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
        /// <param name="fileContent">Raw text content of exclusivity.txt.</param>
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
                    _logger.LogWarning("Skipping malformed exclusivity row {LineNumber}: expected {Expected} columns, got {Actual}",
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
        /// Parses a date string in "MMM d, yyyy" format (e.g., "Jul 13, 2026", "Jun 28, 2027")
        /// into a <see cref="DateTime"/>. Returns null for blank, empty, or unparseable values.
        /// </summary>
        /// <param name="dateText">The raw date string from exclusivity.txt.</param>
        /// <returns>The parsed date, or null if the text is empty or unparseable.</returns>
        /// <seealso cref="OrangeBook.Exclusivity.ExclusivityDate"/>
        private DateTime? parseDate(string dateText)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(dateText))
                return null;

            var trimmed = dateText.Trim();

            // Dates appear as "MMM d, yyyy" or "MMM dd, yyyy" (e.g., "Jul 13, 2026", "Jun 28, 2027")
            string[] formats = { "MMM dd, yyyy", "MMM d, yyyy" };

            if (DateTime.TryParseExact(trimmed, formats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }

            _logger.LogWarning("Could not parse exclusivity date: '{DateText}'", trimmed);
            return null;

            #endregion
        }

        #endregion

        #region product lookup

        /*************************************************************/
        /// <summary>
        /// Loads all <see cref="OrangeBook.Product"/> records into a dictionary keyed by
        /// the composite natural key (ApplType, ApplNo, ProductNo), mapping to OrangeBookProductID.
        /// Used for resolving the foreign key when importing exclusivity records.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary mapping (ApplType, ApplNo, ProductNo) to OrangeBookProductID.</returns>
        /// <seealso cref="OrangeBook.Product"/>
        /// <seealso cref="OrangeBook.Exclusivity.OrangeBookProductID"/>
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

        #region exclusivity upsert

        /*************************************************************/
        /// <summary>
        /// Upserts exclusivity records in batches, creating new <see cref="OrangeBook.Exclusivity"/>
        /// entities or updating existing ones. The upsert natural key is
        /// (ApplType, ApplNo, ProductNo, ExclusivityCode). Each exclusivity record is linked
        /// to its parent product via the <paramref name="productLookup"/> dictionary.
        /// </summary>
        /// <param name="dataRows">Validated row arrays from <see cref="parseLines"/>.</param>
        /// <param name="productLookup">Dictionary mapping product natural key to OrangeBookProductID.</param>
        /// <param name="context">The database context for the current import scope.</param>
        /// <param name="result">Import result to track exclusivity counts.</param>
        /// <param name="reportProgress">Optional progress reporting callback.</param>
        /// <param name="token">Cancellation token.</param>
        /// <seealso cref="OrangeBook.Exclusivity"/>
        /// <seealso cref="OrangeBook.Product"/>
        private async Task upsertExclusivityAsync(
            List<string[]> dataRows,
            Dictionary<(string, string, string), int> productLookup,
            ApplicationDbContext context,
            OrangeBookImportResult result,
            Action<string>? reportProgress,
            CancellationToken token)
        {
            #region implementation

            // Load all existing exclusivity records into a lookup by natural key
            // (ApplType, ApplNo, ProductNo, ExclusivityCode)
            var existingExclusivity = await context.Set<OrangeBook.Exclusivity>()
                .ToDictionaryAsync(
                    e => (e.ApplType ?? string.Empty, e.ApplNo ?? string.Empty,
                          e.ProductNo ?? string.Empty, e.ExclusivityCode ?? string.Empty),
                    e => e,
                    token);

            // Track unlinked product keys to avoid repeated warnings for the same key
            var warnedProductKeys = new HashSet<(string, string, string)>();

            // Process in batches
            int totalRows = dataRows.Count;
            int batchNumber = 0;

            for (int offset = 0; offset < totalRows; offset += EXCLUSIVITY_BATCH_SIZE)
            {
                token.ThrowIfCancellationRequested();

                batchNumber++;
                int batchEnd = Math.Min(offset + EXCLUSIVITY_BATCH_SIZE, totalRows);
                int batchCreated = 0;
                int batchUpdated = 0;

                for (int i = offset; i < batchEnd; i++)
                {
                    var row = dataRows[i];
                    var applType = row[COL_APPL_TYPE]?.Trim() ?? string.Empty;
                    var applNo = row[COL_APPL_NO]?.Trim() ?? string.Empty;
                    var productNo = row[COL_PRODUCT_NO]?.Trim() ?? string.Empty;
                    var exclusivityCode = row[COL_EXCLUSIVITY_CODE]?.Trim() ?? string.Empty;
                    var naturalKey = (applType, applNo, productNo, exclusivityCode);

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
                        _logger.LogWarning("No matching product for exclusivity row: ApplType={ApplType}, ApplNo={ApplNo}, ProductNo={ProductNo}",
                            applType, applNo, productNo);
                    }

                    // Parse field values
                    var exclusivityDate = parseDate(row[COL_EXCLUSIVITY_DATE]);

                    if (existingExclusivity.TryGetValue(naturalKey, out var existing))
                    {
                        // Update all fields on the tracked entity
                        existing.OrangeBookProductID = productId;
                        existing.ExclusivityDate = exclusivityDate;
                        batchUpdated++;

                        if (productId.HasValue)
                            result.ExclusivityLinkedToProduct++;
                        else
                            result.UnlinkedExclusivity++;
                    }
                    else
                    {
                        // Insert new exclusivity record
                        var newExclusivity = new OrangeBook.Exclusivity
                        {
                            ApplType = applType,
                            ApplNo = applNo,
                            ProductNo = productNo,
                            ExclusivityCode = exclusivityCode,
                            OrangeBookProductID = productId,
                            ExclusivityDate = exclusivityDate
                        };

                        context.Set<OrangeBook.Exclusivity>().Add(newExclusivity);

                        // Track for the lookup so subsequent batches know this key exists
                        existingExclusivity[naturalKey] = newExclusivity;
                        batchCreated++;

                        if (productId.HasValue)
                            result.ExclusivityLinkedToProduct++;
                        else
                            result.UnlinkedExclusivity++;
                    }
                }

                // Save the batch
                await context.SaveChangesAsync(token);

                result.ExclusivityCreated += batchCreated;
                result.ExclusivityUpdated += batchUpdated;

                reportProgress?.Invoke($"Exclusivity batch {batchNumber}: {batchCreated} created, {batchUpdated} updated " +
                    $"({Math.Min(batchEnd, totalRows)}/{totalRows} rows processed)");

                // Clear change tracker to free memory between batches
                context.ChangeTracker.Clear();

                // Re-load existing exclusivity records for the next batch since tracker was cleared
                if (batchEnd < totalRows)
                {
                    existingExclusivity = await context.Set<OrangeBook.Exclusivity>()
                        .ToDictionaryAsync(
                            e => (e.ApplType ?? string.Empty, e.ApplNo ?? string.Empty,
                                  e.ProductNo ?? string.Empty, e.ExclusivityCode ?? string.Empty),
                            e => e,
                            token);
                }
            }

            #endregion
        }

        #endregion
    }
}
