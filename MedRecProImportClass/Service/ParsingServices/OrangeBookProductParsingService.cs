using MedRecProImportClass.Data;
using MedRecProImportClass.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.RegularExpressions;
using static MedRecProImportClass.Models.Label;

namespace MedRecProImportClass.Service.ParsingServices
{
    /*************************************************************/
    /// <summary>
    /// Parses the FDA Orange Book products.txt flat file and upserts data into the
    /// <see cref="OrangeBook.Applicant"/>, <see cref="OrangeBook.Product"/>,
    /// <see cref="OrangeBook.ApplicantOrganization"/>, <see cref="OrangeBook.ProductIngredientSubstance"/>,
    /// and <see cref="OrangeBook.ProductMarketingCategory"/> tables. The import is idempotent:
    /// existing records are updated and new records are inserted, allowing the calling
    /// console app to fail and resume without data loss or duplication.
    /// </summary>
    /// <remarks>
    /// The products.txt file uses tilde (~) as the field delimiter with a header row.
    /// The service runs on its own processing thread via a scoped
    /// <see cref="ApplicationDbContext"/> created from <see cref="IServiceScopeFactory"/>.
    /// Products are processed in batches of 5,000 to manage memory.
    /// Entity-to-entity matching uses a tiered strategy: exact name and substring
    /// containment for Product→IngredientSubstance and Product→MarketingCategory,
    /// and normalized exact + token-based Jaccard/containment similarity for
    /// Applicant→Organization (with corporate suffix and pharma noise-word stripping).
    /// </remarks>
    /// <seealso cref="OrangeBook"/>
    /// <seealso cref="OrangeBook.Product"/>
    /// <seealso cref="OrangeBook.Applicant"/>
    /// <seealso cref="OrangeBook.ApplicantOrganization"/>
    /// <seealso cref="OrangeBook.ProductIngredientSubstance"/>
    /// <seealso cref="OrangeBook.ProductMarketingCategory"/>
    /// <seealso cref="Organization"/>
    /// <seealso cref="SplImportService"/>
    public class OrangeBookProductParsingService
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
        /// <seealso cref="ILogger{OrangeBookProductParsingService}"/>
        private readonly ILogger<OrangeBookProductParsingService> _logger;

        #endregion

        #region constants

        private const char FIELD_DELIMITER = '~';
        private const char DF_ROUTE_SEPARATOR = ';';
        private const string PREMARKET_DATE_TEXT = "Approved Prior to Jan 1, 1982";
        private const int EXPECTED_COLUMN_COUNT = 14;
        private const int PRODUCT_BATCH_SIZE = 5000;

        // Column indices (zero-based) for the tilde-delimited products.txt
        private const int COL_INGREDIENT = 0;
        private const int COL_DF_ROUTE = 1;
        private const int COL_TRADE_NAME = 2;
        private const int COL_APPLICANT = 3;
        private const int COL_STRENGTH = 4;
        private const int COL_APPL_TYPE = 5;
        private const int COL_APPL_NO = 6;
        private const int COL_PRODUCT_NO = 7;
        private const int COL_TE_CODE = 8;
        private const int COL_APPROVAL_DATE = 9;
        private const int COL_RLD = 10;
        private const int COL_RS = 11;
        private const int COL_TYPE = 12;
        private const int COL_APPLICANT_FULL_NAME = 13;

        // Token-similarity thresholds for Applicant→Organization matching
        private const double TOKEN_SIMILARITY_THRESHOLD = 0.67;
        private const int MIN_TOKEN_LENGTH_FOR_FUZZY = 3;

        // Compiled regex patterns for company name normalization
        private static readonly Regex _corporateSuffixPattern = new Regex(
            @"\b(INCORPORATED|CORPORATION|INTERNATIONAL|COMPANY|LIMITED|INC|CORP|LLC|LTD|CO|LP|LLP|AG|GMBH|SA|NV|PLC|SE|SRL|BV|SARL|PTY|USA|US|INTL|LC)\b",
            RegexOptions.Compiled);

        private static readonly Regex _pharmaNoisePattern = new Regex(
            @"\b(PHARMACEUTICALS|PHARMACEUTICAL|PHARMA|PHARMS|PHARM|LABORATORIES|LABORATORY|LABS|LAB|PRODUCTS|PRODUCT|HOLDINGS|HOLDING|GROUP|ENTERPRISES|ENTERPRISE|INDUSTRIES|INDUSTRY|SCIENCES|SCIENCE|THERAPEUTICS|BIOSCIENCES|BIOTECH|BIOTECHNOLOGY)\b",
            RegexOptions.Compiled);

        private static readonly Regex _nonAlphanumericPattern = new Regex(
            @"[^A-Z0-9 ]", RegexOptions.Compiled);

        private static readonly Regex _multiSpacePattern = new Regex(
            @"\s{2,}", RegexOptions.Compiled);

        #endregion

        #region types

        /*************************************************************/
        /// <summary>
        /// Pre-computed cache entry for an <see cref="Organization"/> record, holding
        /// the normalized name forms and tokenized representation used during
        /// applicant-to-organization matching.
        /// </summary>
        /// <param name="OrganizationID">The organization's primary key.</param>
        /// <param name="NormalizedName">Upper-cased name with corporate suffixes and punctuation stripped.</param>
        /// <param name="NormalizedNameStripped">Like <paramref name="NormalizedName"/> but also stripped of pharma noise words.</param>
        /// <param name="Tokens">Word tokens derived from <paramref name="NormalizedNameStripped"/> (noise-word stripped).</param>
        /// <param name="NonStrippedTokens">Word tokens derived from <paramref name="NormalizedName"/> (suffix-stripped only, preserves noise words).</param>
        /// <seealso cref="Organization"/>
        /// <seealso cref="normalizeCompanyName"/>
        private readonly record struct OrgCacheEntry(
            int OrganizationID,
            string NormalizedName,
            string NormalizedNameStripped,
            HashSet<string> Tokens,
            HashSet<string> NonStrippedTokens);

        #endregion

        /*************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="OrangeBookProductParsingService"/>
        /// with the required dependency injection services.
        /// </summary>
        /// <param name="scopeFactory">Factory for creating scoped service providers on the processing thread.</param>
        /// <param name="logger">Logger for tracking import operations and errors.</param>
        /// <example>
        /// <code>
        /// // Typically registered via DI:
        /// services.AddScoped&lt;OrangeBookProductParsingService&gt;();
        /// </code>
        /// </example>
        /// <seealso cref="IServiceScopeFactory"/>
        /// <seealso cref="ILogger{OrangeBookProductParsingService}"/>
        public OrangeBookProductParsingService(
            IServiceScopeFactory scopeFactory,
            ILogger<OrangeBookProductParsingService> logger)
        {
            #region implementation
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Parses the contents of products.txt and upserts all Applicant, Product, and
        /// ApplicantOrganization records. Existing records are updated; new records are inserted.
        /// The operation is idempotent and safe to re-run after a failure.
        /// </summary>
        /// <param name="fileContent">The full text content of products.txt (tilde-delimited with header row).</param>
        /// <param name="token">Cancellation token for cooperative cancellation.</param>
        /// <param name="reportProgress">Optional delegate for progress reporting.</param>
        /// <returns>An <see cref="OrangeBookImportResult"/> with counts and error details.</returns>
        /// <example>
        /// <code>
        /// var service = scope.ServiceProvider.GetRequiredService&lt;OrangeBookProductParsingService&gt;();
        /// var result = await service.ProcessProductsFileAsync(productsText, cancellationToken);
        /// Console.WriteLine($"Created {result.ProductsCreated}, Updated {result.ProductsUpdated}");
        /// </code>
        /// </example>
        /// <remarks>
        /// Creates its own <see cref="IServiceScope"/> and <see cref="ApplicationDbContext"/>
        /// for thread safety. The database connection is manually opened and held open for
        /// the duration of the import to avoid repeated open/close overhead (following the
        /// pattern in <see cref="SplImportService"/>).
        /// </remarks>
        /// <seealso cref="OrangeBookImportResult"/>
        /// <seealso cref="OrangeBook.Product"/>
        /// <seealso cref="OrangeBook.Applicant"/>
        /// <seealso cref="OrangeBook.ApplicantOrganization"/>
        public async Task<OrangeBookImportResult> ProcessProductsFileAsync(
            string fileContent,
            CancellationToken token,
            Action<string>? reportProgress = null)
        {
            #region implementation
            var result = new OrangeBookImportResult();

            if (string.IsNullOrWhiteSpace(fileContent))
            {
                result.Success = false;
                result.Errors.Add("File content is null or empty.");
                result.Message = "No content to process.";
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
                reportProgress?.Invoke("Parsing products.txt lines...");
                var dataRows = parseLines(fileContent, result);
                _logger.LogInformation("Parsed {RowCount} data rows from products.txt ({Skipped} malformed rows skipped)",
                    dataRows.Count, result.MalformedRowsSkipped);

                if (dataRows.Count == 0)
                {
                    result.Success = false;
                    result.Errors.Add("No valid data rows found in products.txt.");
                    result.Message = "No data to import.";
                    return result;
                }

                token.ThrowIfCancellationRequested();

                // Step 2: Upsert unique applicants
                reportProgress?.Invoke("Upserting applicants...");
                var applicantMap = await upsertApplicantsAsync(dataRows, context, result, token);
                _logger.LogInformation("Applicants: {Created} created, {Updated} updated",
                    result.ApplicantsCreated, result.ApplicantsUpdated);

                token.ThrowIfCancellationRequested();

                // Step 3: Upsert products in batches
                reportProgress?.Invoke("Upserting products...");
                var productIdMap = await upsertProductsAsync(dataRows, applicantMap, context, result, reportProgress, token);
                _logger.LogInformation("Products: {Created} created, {Updated} updated",
                    result.ProductsCreated, result.ProductsUpdated);

                token.ThrowIfCancellationRequested();

                // Step 4: Match applicants to existing Organization records
                reportProgress?.Invoke("Matching applicants to organizations...");
                await matchApplicantsToOrganizationsAsync(applicantMap, context, result, token);
                _logger.LogInformation("Organization matches: {Created} created, {Unmatched} applicants unmatched",
                    result.OrganizationMatchesCreated, result.UnmatchedApplicants);

                token.ThrowIfCancellationRequested();

                // Step 5: Match products to existing IngredientSubstance records
                reportProgress?.Invoke("Matching products to ingredient substances...");
                var productIngredientMap = buildProductIngredientMap(dataRows, productIdMap);
                await matchProductsToIngredientSubstancesAsync(productIngredientMap, context, result, token, reportProgress);
                _logger.LogInformation("Ingredient substance matches: {Created} created, {Unmatched} ingredients unmatched",
                    result.IngredientSubstanceMatchesCreated, result.UnmatchedIngredients);

                token.ThrowIfCancellationRequested();

                // Step 6: Match products to existing MarketingCategory records
                reportProgress?.Invoke("Matching products to marketing categories...");
                var productAppNumberMap = buildProductAppNumberMap(dataRows, productIdMap);
                await matchProductsToMarketingCategoriesAsync(productAppNumberMap, context, result, token, reportProgress);
                _logger.LogInformation("Marketing category matches: {Created} created, {Unmatched} products unmatched",
                    result.MarketingCategoryMatchesCreated, result.UnmatchedProducts);

                // Final status
                result.Success = result.Errors.Count == 0;
                result.Message = result.Success
                    ? $"Import completed. {result.ApplicantsCreated + result.ApplicantsUpdated} applicants, " +
                      $"{result.ProductsCreated + result.ProductsUpdated} products, " +
                      $"{result.OrganizationMatchesCreated} organization matches, " +
                      $"{result.IngredientSubstanceMatchesCreated} ingredient matches, " +
                      $"{result.MarketingCategoryMatchesCreated} marketing category matches."
                    : "Import completed with errors.";

                reportProgress?.Invoke(result.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Orange Book products import was cancelled.");
                result.Success = false;
                result.Errors.Add("Import was cancelled.");
                result.Message = "Import cancelled.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during Orange Book products import.");
                result.Success = false;
                result.Errors.Add($"Critical error: {ex.Message}");
                result.Message = "Import failed with critical error.";
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
        /// <param name="fileContent">Raw text content of products.txt.</param>
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
                    _logger.LogWarning("Skipping malformed row {LineNumber}: expected {Expected} columns, got {Actual}",
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
        /// Splits the combined DF;Route column value into separate DosageForm and Route strings.
        /// The source column format is "DOSAGE_FORM;ROUTE" (e.g., "AEROSOL, FOAM;RECTAL").
        /// </summary>
        /// <param name="dfRouteValue">The combined dosage form and route string from column 1.</param>
        /// <returns>A tuple of (dosageForm, route). If no semicolon is found, the entire
        /// value becomes DosageForm and Route is null.</returns>
        /// <example>
        /// <code>
        /// var (form, route) = splitDfRoute("AEROSOL, FOAM;RECTAL");
        /// // form = "AEROSOL, FOAM", route = "RECTAL"
        /// </code>
        /// </example>
        /// <seealso cref="OrangeBook.Product.DosageForm"/>
        /// <seealso cref="OrangeBook.Product.Route"/>
        private (string? dosageForm, string? route) splitDfRoute(string dfRouteValue)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(dfRouteValue))
                return (null, null);

            var trimmed = dfRouteValue.Trim();

            // Split on the LAST semicolon to handle dosage forms that contain semicolons
            // (e.g., "SOLUTION/DROPS;OPHTHALMIC" is straightforward, but
            // some dosage forms may contain semicolons in complex descriptions)
            var separatorIndex = trimmed.LastIndexOf(DF_ROUTE_SEPARATOR);

            if (separatorIndex < 0)
            {
                // No semicolon found — entire value is dosage form
                return (trimmed, null);
            }

            var dosageForm = trimmed.Substring(0, separatorIndex).Trim();
            var route = trimmed.Substring(separatorIndex + 1).Trim();

            return (
                string.IsNullOrWhiteSpace(dosageForm) ? null : dosageForm,
                string.IsNullOrWhiteSpace(route) ? null : route
            );
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Parses the Approval_Date column value into a <see cref="DateTime"/> or null.
        /// The special text "Approved Prior to Jan 1, 1982" sets <paramref name="isPremarket"/>
        /// to true and returns null. Standard dates use "MMM d, yyyy" format.
        /// </summary>
        /// <param name="dateText">The raw date string from column 9.</param>
        /// <param name="isPremarket">Set to true when the date text indicates pre-1982 approval.</param>
        /// <returns>The parsed date, or null if the text is empty, pre-market, or unparseable.</returns>
        /// <seealso cref="OrangeBook.Product.ApprovalDate"/>
        /// <seealso cref="OrangeBook.Product.ApprovalDateIsPremarket"/>
        private DateTime? parseApprovalDate(string dateText, out bool isPremarket)
        {
            #region implementation
            isPremarket = false;

            if (string.IsNullOrWhiteSpace(dateText))
                return null;

            var trimmed = dateText.Trim();

            // Check for the special pre-market approval text
            if (trimmed.Equals(PREMARKET_DATE_TEXT, StringComparison.OrdinalIgnoreCase))
            {
                isPremarket = true;
                return null;
            }

            // Dates appear as "MMM d, yyyy" or "MMM dd, yyyy" (e.g., "Apr 12, 2023", "Oct 7, 2014")
            string[] formats = { "MMM dd, yyyy", "MMM d, yyyy" };

            if (DateTime.TryParseExact(trimmed, formats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }

            _logger.LogWarning("Could not parse approval date: '{DateText}'", trimmed);
            return null;
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Converts "Yes"/"No" string values to boolean. Returns false for any value
        /// that is not "Yes" (case-insensitive comparison).
        /// </summary>
        /// <param name="value">The raw "Yes" or "No" string from the RLD or RS column.</param>
        /// <returns>True if the value is "Yes"; false otherwise.</returns>
        /// <seealso cref="OrangeBook.Product.IsRLD"/>
        /// <seealso cref="OrangeBook.Product.IsRS"/>
        private bool parseYesNo(string value)
        {
            #region implementation
            return !string.IsNullOrWhiteSpace(value)
                && value.Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase);
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Maps the single-character ApplType code from products.txt to the full application
        /// type prefix used in SPL MarketingCategory application numbers.
        /// </summary>
        /// <param name="applType">The ApplType code: "N" (NDA) or "A" (ANDA).</param>
        /// <returns>The full prefix string (e.g., "NDA", "ANDA") or the raw value if unknown.</returns>
        /// <seealso cref="Label.MarketingCategory.ApplicationOrMonographIDValue"/>
        private string mapApplTypeToPrefix(string applType)
        {
            #region implementation
            return applType?.Trim().ToUpper() switch
            {
                "N" => "NDA",
                "A" => "ANDA",
                _ => applType?.Trim() ?? string.Empty
            };
            #endregion
        }

        #endregion

        #region applicant upsert

        /*************************************************************/
        /// <summary>
        /// Extracts unique applicants from the data rows and upserts them into the
        /// <see cref="OrangeBook.Applicant"/> table. Existing applicants are updated
        /// if their full name has changed; new applicants are inserted.
        /// </summary>
        /// <param name="dataRows">Parsed data rows from products.txt.</param>
        /// <param name="context">The database context for the current import scope.</param>
        /// <param name="result">Import result to track created/updated counts.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A dictionary mapping ApplicantName to OrangeBookApplicantID for product FK resolution.</returns>
        /// <seealso cref="OrangeBook.Applicant"/>
        /// <seealso cref="OrangeBook.Product.OrangeBookApplicantID"/>
        private async Task<Dictionary<string, int>> upsertApplicantsAsync(
            List<string[]> dataRows,
            ApplicationDbContext context,
            OrangeBookImportResult result,
            CancellationToken token)
        {
            #region implementation
            var fileApplicants = extractUniqueApplicantsFromRows(dataRows);

            var existingApplicants = await context.Set<OrangeBook.Applicant>()
                .ToDictionaryAsync(
                    a => a.ApplicantName ?? string.Empty,
                    a => a,
                    StringComparer.OrdinalIgnoreCase,
                    token);

            applyApplicantUpserts(fileApplicants, existingApplicants, context, result);

            await context.SaveChangesAsync(token);

            return buildApplicantIdMap(existingApplicants);
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Scans the parsed data rows and extracts a deduplicated dictionary of applicants,
        /// keyed on the short name (column 3) with the full legal name (column 13) as value.
        /// The first full name encountered for each short name is kept.
        /// </summary>
        /// <param name="dataRows">Parsed data rows from products.txt.</param>
        /// <returns>Dictionary mapping short applicant name to full legal name (nullable).</returns>
        /// <seealso cref="OrangeBook.Applicant.ApplicantName"/>
        /// <seealso cref="OrangeBook.Applicant.ApplicantFullName"/>
        private Dictionary<string, string?> extractUniqueApplicantsFromRows(List<string[]> dataRows)
        {
            #region implementation
            var fileApplicants = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in dataRows)
            {
                var shortName = row[COL_APPLICANT]?.Trim();

                if (string.IsNullOrWhiteSpace(shortName))
                    continue;

                // Keep the first full name encountered for each short name
                if (!fileApplicants.ContainsKey(shortName))
                {
                    var fullName = row[COL_APPLICANT_FULL_NAME]?.Trim();
                    fileApplicants[shortName] = string.IsNullOrWhiteSpace(fullName) ? null : fullName;
                }
            }

            return fileApplicants;
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Compares the file-sourced applicants against existing database records and applies
        /// updates or inserts as needed via EF Core change tracking.
        /// </summary>
        /// <param name="fileApplicants">Unique applicants extracted from the file (shortName → fullName).</param>
        /// <param name="existingApplicants">Tracked entities loaded from the database (shortName → entity).</param>
        /// <param name="context">The database context for adding new entities.</param>
        /// <param name="result">Import result to track created/updated counts.</param>
        /// <seealso cref="OrangeBook.Applicant"/>
        private void applyApplicantUpserts(
            Dictionary<string, string?> fileApplicants,
            Dictionary<string, OrangeBook.Applicant> existingApplicants,
            ApplicationDbContext context,
            OrangeBookImportResult result)
        {
            #region implementation
            foreach (var (shortName, fullName) in fileApplicants)
            {
                if (existingApplicants.TryGetValue(shortName, out var existing))
                {
                    // Update the full name if it has changed
                    if (!string.Equals(existing.ApplicantFullName, fullName, StringComparison.Ordinal))
                    {
                        existing.ApplicantFullName = fullName;
                        result.ApplicantsUpdated++;
                    }
                }
                else
                {
                    // Insert new applicant
                    var newApplicant = new OrangeBook.Applicant
                    {
                        ApplicantName = shortName,
                        ApplicantFullName = fullName
                    };

                    context.Set<OrangeBook.Applicant>().Add(newApplicant);
                    existingApplicants[shortName] = newApplicant;
                    result.ApplicantsCreated++;
                }
            }
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Builds a dictionary mapping ApplicantName to OrangeBookApplicantID from
        /// the tracked entities (whose IDs are populated after SaveChangesAsync).
        /// </summary>
        /// <param name="existingApplicants">Tracked applicant entities keyed by short name.</param>
        /// <returns>Dictionary mapping ApplicantName to OrangeBookApplicantID.</returns>
        /// <seealso cref="OrangeBook.Applicant"/>
        private Dictionary<string, int> buildApplicantIdMap(
            Dictionary<string, OrangeBook.Applicant> existingApplicants)
        {
            #region implementation
            var applicantMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var (name, entity) in existingApplicants)
            {
                if (entity.OrangeBookApplicantID.HasValue)
                {
                    applicantMap[name] = entity.OrangeBookApplicantID.Value;
                }
            }

            return applicantMap;
            #endregion
        }

        #endregion

        #region product upsert

        /*************************************************************/
        /// <summary>
        /// Upserts products from the parsed data rows into the <see cref="OrangeBook.Product"/>
        /// table. Existing products (matched by composite natural key ApplType + ApplNo + ProductNo)
        /// are updated; new products are inserted. Processing is done in batches to manage memory.
        /// </summary>
        /// <param name="dataRows">Parsed data rows from products.txt.</param>
        /// <param name="applicantMap">Dictionary mapping ApplicantName to OrangeBookApplicantID.</param>
        /// <param name="context">The database context for the current import scope.</param>
        /// <param name="result">Import result to track created/updated counts.</param>
        /// <param name="reportProgress">Optional delegate for progress reporting.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A dictionary mapping (ApplType, ApplNo, ProductNo) to OrangeBookProductID for junction table resolution.</returns>
        /// <seealso cref="OrangeBook.Product"/>
        /// <seealso cref="OrangeBook.Applicant"/>
        private async Task<Dictionary<(string, string, string), int>> upsertProductsAsync(
            List<string[]> dataRows,
            Dictionary<string, int> applicantMap,
            ApplicationDbContext context,
            OrangeBookImportResult result,
            Action<string>? reportProgress,
            CancellationToken token)
        {
            #region implementation

            // Load all existing products into a lookup by natural key (ApplType, ApplNo, ProductNo)
            var existingProducts = await context.Set<OrangeBook.Product>()
                .ToDictionaryAsync(
                    p => (p.ApplType ?? string.Empty, p.ApplNo ?? string.Empty, p.ProductNo ?? string.Empty),
                    p => p,
                    token);

            // Track IDs for products that survive ChangeTracker.Clear() between batches
            var productIdMap = new Dictionary<(string, string, string), int>();

            // Seed the ID map with existing products
            foreach (var (key, product) in existingProducts)
            {
                if (product.OrangeBookProductID.HasValue)
                    productIdMap[key] = product.OrangeBookProductID.Value;
            }

            // Process in batches
            int totalRows = dataRows.Count;
            int batchNumber = 0;

            for (int offset = 0; offset < totalRows; offset += PRODUCT_BATCH_SIZE)
            {
                token.ThrowIfCancellationRequested();

                batchNumber++;
                int batchEnd = Math.Min(offset + PRODUCT_BATCH_SIZE, totalRows);
                int batchCreated = 0;
                int batchUpdated = 0;

                for (int i = offset; i < batchEnd; i++)
                {
                    var row = dataRows[i];
                    var applType = row[COL_APPL_TYPE]?.Trim() ?? string.Empty;
                    var applNo = row[COL_APPL_NO]?.Trim() ?? string.Empty;
                    var productNo = row[COL_PRODUCT_NO]?.Trim() ?? string.Empty;
                    var naturalKey = (applType, applNo, productNo);

                    // Parse field values
                    var (dosageForm, route) = splitDfRoute(row[COL_DF_ROUTE]);
                    var approvalDate = parseApprovalDate(row[COL_APPROVAL_DATE], out var isPremarket);
                    var applicantName = row[COL_APPLICANT]?.Trim();
                    int? applicantId = null;
                    if (!string.IsNullOrWhiteSpace(applicantName)
                        && applicantMap.TryGetValue(applicantName, out var appId))
                    {
                        applicantId = appId;
                    }

                    var teCode = row[COL_TE_CODE]?.Trim();

                    if (existingProducts.TryGetValue(naturalKey, out var existing))
                    {
                        // Update all fields on the tracked entity
                        existing.Ingredient = row[COL_INGREDIENT]?.Trim();
                        existing.DosageForm = dosageForm;
                        existing.Route = route;
                        existing.TradeName = row[COL_TRADE_NAME]?.Trim();
                        existing.Strength = row[COL_STRENGTH]?.Trim();
                        existing.OrangeBookApplicantID = applicantId;
                        existing.TECode = string.IsNullOrWhiteSpace(teCode) ? null : teCode;
                        existing.Type = row[COL_TYPE]?.Trim();
                        existing.ApprovalDate = approvalDate;
                        existing.ApprovalDateIsPremarket = isPremarket;
                        existing.IsRLD = parseYesNo(row[COL_RLD]);
                        existing.IsRS = parseYesNo(row[COL_RS]);
                        batchUpdated++;
                    }
                    else
                    {
                        // Insert new product
                        var newProduct = new OrangeBook.Product
                        {
                            ApplType = applType,
                            ApplNo = applNo,
                            ProductNo = productNo,
                            Ingredient = row[COL_INGREDIENT]?.Trim(),
                            DosageForm = dosageForm,
                            Route = route,
                            TradeName = row[COL_TRADE_NAME]?.Trim(),
                            Strength = row[COL_STRENGTH]?.Trim(),
                            OrangeBookApplicantID = applicantId,
                            TECode = string.IsNullOrWhiteSpace(teCode) ? null : teCode,
                            Type = row[COL_TYPE]?.Trim(),
                            ApprovalDate = approvalDate,
                            ApprovalDateIsPremarket = isPremarket,
                            IsRLD = parseYesNo(row[COL_RLD]),
                            IsRS = parseYesNo(row[COL_RS])
                        };

                        context.Set<OrangeBook.Product>().Add(newProduct);

                        // Track for the lookup so subsequent batches know this key exists
                        existingProducts[naturalKey] = newProduct;
                        batchCreated++;
                    }
                }

                // Save the batch
                await context.SaveChangesAsync(token);

                // Capture newly generated IDs before clearing the tracker
                foreach (var (key, product) in existingProducts)
                {
                    if (product.OrangeBookProductID.HasValue && !productIdMap.ContainsKey(key))
                        productIdMap[key] = product.OrangeBookProductID.Value;
                }

                result.ProductsCreated += batchCreated;
                result.ProductsUpdated += batchUpdated;

                reportProgress?.Invoke($"Products batch {batchNumber}: {batchCreated} created, {batchUpdated} updated " +
                    $"({Math.Min(batchEnd, totalRows)}/{totalRows} rows processed)");

                // Clear change tracker to free memory between batches
                context.ChangeTracker.Clear();

                // Re-load existing products for the next batch since tracker was cleared
                if (batchEnd < totalRows)
                {
                    existingProducts = await context.Set<OrangeBook.Product>()
                        .ToDictionaryAsync(
                            p => (p.ApplType ?? string.Empty, p.ApplNo ?? string.Empty, p.ProductNo ?? string.Empty),
                            p => p,
                            token);
                }
            }

            return productIdMap;
            #endregion
        }

        #endregion

        #region organization matching

        /*************************************************************/
        /// <summary>
        /// Orchestrates the two-tier matching of Orange Book applicants to existing SPL
        /// <see cref="Organization"/> records. New matches are inserted into the
        /// <see cref="OrangeBook.ApplicantOrganization"/> junction table;
        /// existing junction rows are preserved.
        /// </summary>
        /// <param name="applicantMap">Dictionary mapping ApplicantName to OrangeBookApplicantID.</param>
        /// <param name="context">The database context for the current import scope.</param>
        /// <param name="result">Import result to track match counts.</param>
        /// <param name="token">Cancellation token.</param>
        /// <seealso cref="matchByNormalizedExact"/>
        /// <seealso cref="matchByTokenSimilarity"/>
        /// <seealso cref="OrangeBook.ApplicantOrganization"/>
        private async Task matchApplicantsToOrganizationsAsync(
            Dictionary<string, int> applicantMap,
            ApplicationDbContext context,
            OrangeBookImportResult result,
            CancellationToken token)
        {
            #region implementation
            var (existingPairs, applicants, matchedApplicantIds) =
                await loadMatchingStateAsync(context, token);

            // Load all organizations into memory for efficient in-memory matching
            var orgCache = await loadOrganizationCacheAsync(context, token);

            var newJunctions = new List<OrangeBook.ApplicantOrganization>();

            // Tier 1: normalized exact match (suffix-stripped, case-insensitive)
            matchByNormalizedExact(applicants, matchedApplicantIds, existingPairs, newJunctions, orgCache);

            // Tier 2: token-based similarity (best-match-only, noise-word stripped)
            matchByTokenSimilarity(applicants, matchedApplicantIds, existingPairs, newJunctions, orgCache);

            await persistJunctionsAsync(newJunctions, context, result, token);
            logUnmatchedApplicants(applicants, matchedApplicantIds, result);
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Loads the current matching state from the database: existing junction pairs,
        /// all applicants, and a set of applicant IDs that already have junctions.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tuple of existing pairs, applicant entities, and pre-matched IDs.</returns>
        /// <seealso cref="OrangeBook.ApplicantOrganization"/>
        /// <seealso cref="OrangeBook.Applicant"/>
        private async Task<(HashSet<(int, int)> existingPairs, List<OrangeBook.Applicant> applicants, HashSet<int> matchedIds)>
            loadMatchingStateAsync(ApplicationDbContext context, CancellationToken token)
        {
            #region implementation
            var existingJunctions = await context.Set<OrangeBook.ApplicantOrganization>()
                .Select(ao => new { ao.OrangeBookApplicantID, ao.OrganizationID })
                .ToListAsync(token);

            var existingPairs = new HashSet<(int, int)>(
                existingJunctions
                    .Where(j => j.OrangeBookApplicantID.HasValue && j.OrganizationID.HasValue)
                    .Select(j => (j.OrangeBookApplicantID!.Value, j.OrganizationID!.Value)));

            var applicants = await context.Set<OrangeBook.Applicant>()
                .Where(a => a.OrangeBookApplicantID != null)
                .ToListAsync(token);

            // Pre-seed matched set with applicants that already have junction rows
            var matchedIds = new HashSet<int>(existingPairs.Select(p => p.Item1));

            return (existingPairs, applicants, matchedIds);
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Filters the applicant list to only those not yet matched and meeting the
        /// specified predicate (e.g., has full name, has short name).
        /// </summary>
        /// <param name="applicants">All applicant entities.</param>
        /// <param name="matchedIds">Set of already-matched applicant IDs.</param>
        /// <param name="predicate">Additional filter for the applicant entity.</param>
        /// <returns>Applicants eligible for the current matching tier.</returns>
        /// <seealso cref="OrangeBook.Applicant"/>
        private List<OrangeBook.Applicant> getUnmatchedApplicants(
            List<OrangeBook.Applicant> applicants,
            HashSet<int> matchedIds,
            Func<OrangeBook.Applicant, bool> predicate)
        {
            #region implementation
            return applicants
                .Where(a => a.OrangeBookApplicantID.HasValue
                    && !matchedIds.Contains(a.OrangeBookApplicantID.Value)
                    && predicate(a))
                .ToList();
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Tier 1: Normalized exact match of applicant names against organization names.
        /// Corporate suffixes and punctuation are stripped before comparison so that
        /// "Pfizer Inc." matches "PFIZER LABS" when both normalize to the same string.
        /// Operates entirely in-memory against the pre-loaded organization cache.
        /// Tries both <see cref="OrangeBook.Applicant.ApplicantFullName"/> and
        /// <see cref="OrangeBook.Applicant.ApplicantName"/> for each applicant.
        /// </summary>
        /// <param name="applicants">All applicant entities.</param>
        /// <param name="matchedIds">Set of already-matched applicant IDs (mutated on match).</param>
        /// <param name="existingPairs">Set of existing junction pairs (mutated on new junction).</param>
        /// <param name="newJunctions">List of new junction entities to insert (mutated).</param>
        /// <param name="orgCache">Pre-computed organization cache from <see cref="loadOrganizationCacheAsync"/>.</param>
        /// <seealso cref="normalizeCompanyName"/>
        /// <seealso cref="OrgCacheEntry"/>
        private void matchByNormalizedExact(
            List<OrangeBook.Applicant> applicants,
            HashSet<int> matchedIds,
            HashSet<(int, int)> existingPairs,
            List<OrangeBook.ApplicantOrganization> newJunctions,
            List<OrgCacheEntry> orgCache)
        {
            #region implementation
            var candidates = getUnmatchedApplicants(applicants, matchedIds,
                a => !string.IsNullOrWhiteSpace(a.ApplicantFullName)
                    || !string.IsNullOrWhiteSpace(a.ApplicantName));

            if (candidates.Count == 0)
                return;

            // Build lookup: normalized org name (without noise stripping) → list of org IDs
            var orgNameLookup = new Dictionary<string, List<int>>();
            foreach (var entry in orgCache)
            {
                if (string.IsNullOrEmpty(entry.NormalizedName))
                    continue;

                if (!orgNameLookup.TryGetValue(entry.NormalizedName, out var ids))
                {
                    ids = new List<int>();
                    orgNameLookup[entry.NormalizedName] = ids;
                }
                ids.Add(entry.OrganizationID);
            }

            foreach (var applicant in candidates)
            {
                // Try the full name first, then fall back to the short name
                var normalizedFull = !string.IsNullOrWhiteSpace(applicant.ApplicantFullName)
                    ? normalizeCompanyName(applicant.ApplicantFullName!)
                    : string.Empty;

                var normalizedShort = !string.IsNullOrWhiteSpace(applicant.ApplicantName)
                    ? normalizeCompanyName(applicant.ApplicantName!)
                    : string.Empty;

                bool matched = false;

                // Check full name first
                if (!string.IsNullOrEmpty(normalizedFull)
                    && orgNameLookup.TryGetValue(normalizedFull, out var orgIds))
                {
                    foreach (var orgId in orgIds)
                    {
                        addJunctionIfNew(applicant.OrangeBookApplicantID!.Value, orgId,
                            existingPairs, newJunctions);
                    }
                    matched = true;
                }

                // Also check short name (may link to additional orgs)
                if (!string.IsNullOrEmpty(normalizedShort) && normalizedShort != normalizedFull
                    && orgNameLookup.TryGetValue(normalizedShort, out var shortOrgIds))
                {
                    foreach (var orgId in shortOrgIds)
                    {
                        addJunctionIfNew(applicant.OrangeBookApplicantID!.Value, orgId,
                            existingPairs, newJunctions);
                    }
                    matched = true;
                }

                if (matched)
                    matchedIds.Add(applicant.OrangeBookApplicantID!.Value);
            }
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Tier 2: Token-based similarity matching for applicants not matched by
        /// <see cref="matchByNormalizedExact"/>. Uses only <see cref="OrangeBook.Applicant.ApplicantFullName"/>
        /// (the short <see cref="OrangeBook.Applicant.ApplicantName"/> is too abbreviated for fuzzy matching).
        /// Runs a two-pass scoring strategy per applicant:
        /// <list type="number">
        /// <item>Pass 1 — score non-noise-stripped tokens against <see cref="OrgCacheEntry.NonStrippedTokens"/>.
        /// This preserves discriminating words like "THERAPEUTICS" and "LABORATORIES".</item>
        /// <item>Pass 2 (fallback) — if Pass 1 finds no match, score noise-stripped tokens against
        /// <see cref="OrgCacheEntry.Tokens"/>, but only when the stripped form has ≥ 2 tokens
        /// (prevents single generic tokens like "AMERICAN" from matching everything).</item>
        /// </list>
        /// Only the best-scoring organization(s) per applicant are linked (best-match-only policy).
        /// </summary>
        /// <param name="applicants">All applicant entities.</param>
        /// <param name="matchedIds">Set of already-matched applicant IDs (mutated on match).</param>
        /// <param name="existingPairs">Set of existing junction pairs (mutated on new junction).</param>
        /// <param name="newJunctions">List of new junction entities to insert (mutated).</param>
        /// <param name="orgCache">Pre-computed organization cache from <see cref="loadOrganizationCacheAsync"/>.</param>
        /// <remarks>
        /// The similarity score is <c>Max(jaccard, containment)</c> where containment is asymmetric
        /// (fraction of applicant tokens found in the org name). This ensures that an applicant
        /// named "PFIZER" scores 1.0 against an organization named "PFIZER CONSUMER HEALTHCARE"
        /// even though the Jaccard coefficient would be low (1/3).
        /// </remarks>
        /// <seealso cref="normalizeCompanyName"/>
        /// <seealso cref="tokenize"/>
        /// <seealso cref="calculateTokenSimilarity"/>
        /// <seealso cref="TOKEN_SIMILARITY_THRESHOLD"/>
        private void matchByTokenSimilarity(
            List<OrangeBook.Applicant> applicants,
            HashSet<int> matchedIds,
            HashSet<(int, int)> existingPairs,
            List<OrangeBook.ApplicantOrganization> newJunctions,
            List<OrgCacheEntry> orgCache)
        {
            #region implementation
            // Only use ApplicantFullName — the short ApplicantName is too abbreviated for fuzzy matching
            var candidates = getUnmatchedApplicants(applicants, matchedIds,
                a => !string.IsNullOrWhiteSpace(a.ApplicantFullName));

            if (candidates.Count == 0)
                return;

            foreach (var applicant in candidates)
            {
                // Non-stripped form (corporate suffixes removed, noise words preserved)
                var fullNormalized = normalizeCompanyName(applicant.ApplicantFullName!);
                var fullTokens = tokenize(fullNormalized);

                // Skip names that reduce to nothing or a single very short token
                bool fullViable = fullTokens.Count > 0
                    && !(fullTokens.Count == 1 && fullNormalized.Length < MIN_TOKEN_LENGTH_FOR_FUZZY);

                if (!fullViable)
                    continue;

                // ── Pass 1: score non-stripped applicant tokens against non-stripped org tokens ──
                double bestScore = 0.0;
                var bestOrgIds = new List<int>();

                foreach (var org in orgCache)
                {
                    if (org.NonStrippedTokens.Count == 0)
                        continue;

                    var (jaccard, containment) = calculateTokenSimilarity(fullTokens, org.NonStrippedTokens);
                    double score = Math.Max(jaccard, containment);

                    // Track best-match-only
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestOrgIds.Clear();
                        bestOrgIds.Add(org.OrganizationID);
                    }
                    else if (score == bestScore && score > 0.0)
                    {
                        bestOrgIds.Add(org.OrganizationID);
                    }
                }

                // If Pass 1 found a match above threshold, use it
                if (bestScore >= TOKEN_SIMILARITY_THRESHOLD)
                {
                    foreach (var orgId in bestOrgIds)
                    {
                        addJunctionIfNew(applicant.OrangeBookApplicantID!.Value, orgId,
                            existingPairs, newJunctions);
                    }
                    matchedIds.Add(applicant.OrangeBookApplicantID!.Value);
                    continue;
                }

                // ── Pass 2 (fallback): noise-strip and re-score, but require ≥ 2 tokens ──
                var fullStripped = normalizeCompanyName(applicant.ApplicantFullName!, stripNoiseWords: true);
                var strippedTokens = tokenize(fullStripped);

                // Guard: noise stripping must leave ≥ 2 meaningful tokens to prevent
                // single generic tokens like "AMERICAN" from matching everything
                if (strippedTokens.Count < 2)
                    continue;

                bestScore = 0.0;
                bestOrgIds.Clear();

                foreach (var org in orgCache)
                {
                    if (org.Tokens.Count == 0)
                        continue;

                    var (jaccard, containment) = calculateTokenSimilarity(strippedTokens, org.Tokens);
                    double score = Math.Max(jaccard, containment);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestOrgIds.Clear();
                        bestOrgIds.Add(org.OrganizationID);
                    }
                    else if (score == bestScore && score > 0.0)
                    {
                        bestOrgIds.Add(org.OrganizationID);
                    }
                }

                if (bestScore >= TOKEN_SIMILARITY_THRESHOLD)
                {
                    foreach (var orgId in bestOrgIds)
                    {
                        addJunctionIfNew(applicant.OrangeBookApplicantID!.Value, orgId,
                            existingPairs, newJunctions);
                    }
                    matchedIds.Add(applicant.OrangeBookApplicantID!.Value);
                }
            }
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Persists all accumulated junction rows to the database in a single batch.
        /// </summary>
        /// <param name="newJunctions">New junction entities to insert.</param>
        /// <param name="context">The database context.</param>
        /// <param name="result">Import result to track match count.</param>
        /// <param name="token">Cancellation token.</param>
        /// <seealso cref="OrangeBook.ApplicantOrganization"/>
        private async Task persistJunctionsAsync(
            List<OrangeBook.ApplicantOrganization> newJunctions,
            ApplicationDbContext context,
            OrangeBookImportResult result,
            CancellationToken token)
        {
            #region implementation
            if (newJunctions.Count == 0)
                return;

            context.Set<OrangeBook.ApplicantOrganization>().AddRange(newJunctions);
            await context.SaveChangesAsync(token);
            result.OrganizationMatchesCreated = newJunctions.Count;
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Logs applicants that could not be matched to any Organization record
        /// and updates the result count for manual review.
        /// </summary>
        /// <param name="applicants">All applicant entities.</param>
        /// <param name="matchedIds">Set of matched applicant IDs after all tiers.</param>
        /// <param name="result">Import result to track unmatched count.</param>
        /// <seealso cref="OrangeBook.Applicant"/>
        private void logUnmatchedApplicants(
            List<OrangeBook.Applicant> applicants,
            HashSet<int> matchedIds,
            OrangeBookImportResult result)
        {
            #region implementation
            var finalUnmatched = applicants
                .Where(a => a.OrangeBookApplicantID.HasValue
                    && !matchedIds.Contains(a.OrangeBookApplicantID.Value))
                .ToList();

            result.UnmatchedApplicants = finalUnmatched.Count;

            foreach (var unmatched in finalUnmatched)
            {
                _logger.LogWarning("No Organization match found for applicant '{ShortName}' (full: '{FullName}')",
                    unmatched.ApplicantName, unmatched.ApplicantFullName);
            }
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Adds a new <see cref="OrangeBook.ApplicantOrganization"/> junction row to the
        /// pending list if the pair does not already exist in the database or pending list.
        /// </summary>
        /// <param name="applicantId">The OrangeBookApplicantID.</param>
        /// <param name="organizationId">The OrganizationID from the SPL Organization table.</param>
        /// <param name="existingPairs">Set of already-persisted (applicantId, orgId) pairs.</param>
        /// <param name="newJunctions">List of new junction entities to be inserted.</param>
        /// <seealso cref="OrangeBook.ApplicantOrganization"/>
        private void addJunctionIfNew(
            int applicantId,
            int organizationId,
            HashSet<(int, int)> existingPairs,
            List<OrangeBook.ApplicantOrganization> newJunctions)
        {
            #region implementation
            var pair = (applicantId, organizationId);

            if (existingPairs.Contains(pair))
                return;

            // Add to both the pending list and the set to prevent duplicates within the batch
            existingPairs.Add(pair);
            newJunctions.Add(new OrangeBook.ApplicantOrganization
            {
                OrangeBookApplicantID = applicantId,
                OrganizationID = organizationId
            });
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Normalizes a company name for matching by stripping corporate suffixes,
        /// punctuation, and optionally pharmaceutical industry noise words. The result
        /// is an upper-cased, whitespace-collapsed string suitable for exact or
        /// token-based comparison.
        /// </summary>
        /// <param name="name">Raw company name (e.g., "Pfizer Inc." or "MYLAN PHARMACEUTICALS INC").</param>
        /// <param name="stripNoiseWords">
        /// When <c>true</c>, also strips common pharma noise words such as
        /// "PHARMACEUTICALS", "LABS", "THERAPEUTICS", etc. Use for Tier 2 (token similarity)
        /// after Tier 1 (exact) has been attempted without noise stripping.
        /// </param>
        /// <returns>Normalized upper-case name, or empty string if the name reduces to nothing.</returns>
        /// <example>
        /// <code>
        /// normalizeCompanyName("Pfizer Inc.")                     // "PFIZER"
        /// normalizeCompanyName("MYLAN PHARMACEUTICALS INC")       // "MYLAN PHARMACEUTICALS"
        /// normalizeCompanyName("MYLAN PHARMACEUTICALS INC", true) // "MYLAN"
        /// normalizeCompanyName("G&amp;G MEDICAL")                 // "GG MEDICAL"
        /// </code>
        /// </example>
        /// <seealso cref="_corporateSuffixPattern"/>
        /// <seealso cref="_pharmaNoisePattern"/>
        private static string normalizeCompanyName(string name, bool stripNoiseWords = false)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // Upper-case for case-insensitive comparison
            var result = name.ToUpperInvariant();

            // Strip HTML-encoded and literal ampersands
            result = result.Replace("&AMP;", " ").Replace("&", " ");

            // Strip corporate suffixes (Inc, LLC, Corp, AG, Ltd, etc.)
            result = _corporateSuffixPattern.Replace(result, " ");

            // Strip punctuation — keep only letters, digits, spaces
            result = _nonAlphanumericPattern.Replace(result, " ");

            // Collapse runs of whitespace and trim
            result = _multiSpacePattern.Replace(result, " ").Trim();

            // Optionally strip pharmaceutical noise words (Labs, Pharma, etc.)
            if (stripNoiseWords)
            {
                result = _pharmaNoisePattern.Replace(result, " ");
                result = _multiSpacePattern.Replace(result, " ").Trim();
            }

            // Strip leading/trailing "THE"
            if (result.StartsWith("THE "))
                result = result.Substring(4);
            if (result.EndsWith(" THE"))
                result = result.Substring(0, result.Length - 4);

            return result.Trim();
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Splits a normalized company name into a set of unique word tokens.
        /// </summary>
        /// <param name="normalizedName">A name previously processed by <see cref="normalizeCompanyName"/>.</param>
        /// <returns>A <see cref="HashSet{T}"/> of word tokens, or an empty set if the name is blank.</returns>
        /// <seealso cref="normalizeCompanyName"/>
        /// <seealso cref="calculateTokenSimilarity"/>
        private static HashSet<string> tokenize(string normalizedName)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(normalizedName))
                return new HashSet<string>();

            return new HashSet<string>(
                normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Calculates token-based similarity between an applicant name and an organization name.
        /// Returns both the symmetric Jaccard coefficient and the asymmetric containment score
        /// (fraction of applicant tokens found in the organization name).
        /// </summary>
        /// <param name="applicantTokens">Word tokens from the normalized applicant name.</param>
        /// <param name="orgTokens">Word tokens from the normalized organization name.</param>
        /// <returns>
        /// A tuple of (jaccard, containment) where both are in [0.0, 1.0].
        /// Returns (0.0, 0.0) if either set is empty.
        /// </returns>
        /// <remarks>
        /// Jaccard = |A ∩ B| / |A ∪ B| — symmetric measure of set overlap.
        /// Containment = |A ∩ B| / |A| — asymmetric; 1.0 means all applicant tokens appear in the org name.
        /// The caller should use Max(jaccard, containment) so that "PFIZER" fully contained in
        /// "PFIZER CONSUMER HEALTHCARE" scores 1.0 despite low Jaccard.
        /// </remarks>
        /// <seealso cref="tokenize"/>
        /// <seealso cref="matchByTokenSimilarity"/>
        private static (double jaccard, double containment) calculateTokenSimilarity(
            HashSet<string> applicantTokens, HashSet<string> orgTokens)
        {
            #region implementation
            if (applicantTokens.Count == 0 || orgTokens.Count == 0)
                return (0.0, 0.0);

            // Count intersection without allocating a new set
            int intersectionCount = 0;
            foreach (var token in applicantTokens)
            {
                if (orgTokens.Contains(token))
                    intersectionCount++;
            }

            int unionCount = applicantTokens.Count + orgTokens.Count - intersectionCount;

            double jaccard = (double)intersectionCount / unionCount;
            double containment = (double)intersectionCount / applicantTokens.Count;

            return (jaccard, containment);
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Loads all <see cref="Organization"/> records from the database and pre-computes
        /// normalized name forms and token sets for efficient in-memory matching.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of <see cref="OrgCacheEntry"/> records ready for matching.</returns>
        /// <seealso cref="OrgCacheEntry"/>
        /// <seealso cref="normalizeCompanyName"/>
        /// <seealso cref="tokenize"/>
        private async Task<List<OrgCacheEntry>> loadOrganizationCacheAsync(
            ApplicationDbContext context, CancellationToken token)
        {
            #region implementation
            var orgs = await context.Set<Organization>()
                .Where(o => o.OrganizationName != null && o.OrganizationID != null)
                .Select(o => new { o.OrganizationID, o.OrganizationName })
                .ToListAsync(token);

            var cache = new List<OrgCacheEntry>(orgs.Count);

            foreach (var org in orgs)
            {
                var normalized = normalizeCompanyName(org.OrganizationName!);
                var stripped = normalizeCompanyName(org.OrganizationName!, stripNoiseWords: true);
                var tokens = tokenize(stripped);
                var nonStrippedTokens = tokenize(normalized);

                // Skip organizations that normalize to empty (e.g., name is just "Inc.")
                if (string.IsNullOrEmpty(normalized))
                    continue;

                cache.Add(new OrgCacheEntry(
                    org.OrganizationID!.Value,
                    normalized,
                    stripped,
                    tokens,
                    nonStrippedTokens));
            }

            _logger.LogInformation("Loaded {Count} organizations into matching cache", cache.Count);

            return cache;
            #endregion
        }

        #endregion

        #region ingredient substance matching

        /*************************************************************/
        /// <summary>
        /// Builds a mapping of OrangeBookProductID to the individual ingredient names
        /// parsed from the semicolon-delimited Ingredient column. Each product may have
        /// one or more ingredient names.
        /// </summary>
        /// <param name="dataRows">Parsed data rows from products.txt.</param>
        /// <param name="productIdMap">Dictionary mapping (ApplType, ApplNo, ProductNo) to OrangeBookProductID.</param>
        /// <returns>Dictionary mapping OrangeBookProductID to a list of trimmed, deduplicated ingredient names.</returns>
        /// <seealso cref="OrangeBook.Product.Ingredient"/>
        /// <seealso cref="OrangeBook.ProductIngredientSubstance"/>
        private Dictionary<int, List<string>> buildProductIngredientMap(
            List<string[]> dataRows,
            Dictionary<(string, string, string), int> productIdMap)
        {
            #region implementation
            var map = new Dictionary<int, List<string>>();

            foreach (var row in dataRows)
            {
                var naturalKey = (
                    row[COL_APPL_TYPE]?.Trim() ?? string.Empty,
                    row[COL_APPL_NO]?.Trim() ?? string.Empty,
                    row[COL_PRODUCT_NO]?.Trim() ?? string.Empty);

                if (!productIdMap.TryGetValue(naturalKey, out var productId))
                    continue;

                var ingredientField = row[COL_INGREDIENT]?.Trim();
                if (string.IsNullOrWhiteSpace(ingredientField))
                    continue;

                // Split semicolon-delimited ingredients and deduplicate within the product
                var ingredients = ingredientField
                    .Split(';')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (ingredients.Count > 0 && !map.ContainsKey(productId))
                {
                    map[productId] = ingredients;
                }
            }

            return map;
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Orchestrates the tiered matching of Orange Book product ingredients to existing
        /// SPL <see cref="Label.IngredientSubstance"/> records. Matching is per-ingredient:
        /// each individual ingredient name within a product is tracked independently through
        /// the tiers. New matches are inserted into the
        /// <see cref="OrangeBook.ProductIngredientSubstance"/> junction table.
        /// </summary>
        /// <param name="productIngredientMap">Dictionary mapping OrangeBookProductID to ingredient name lists.</param>
        /// <param name="context">The database context for the current import scope.</param>
        /// <param name="result">Import result to track match counts.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="reportProgress">Optional progress callback.</param>
        /// <seealso cref="matchIngredientsByExactName"/>
        /// <seealso cref="matchIngredientsBySubstringAsync"/>
        /// <seealso cref="OrangeBook.ProductIngredientSubstance"/>
        private async Task matchProductsToIngredientSubstancesAsync(
            Dictionary<int, List<string>> productIngredientMap,
            ApplicationDbContext context,
            OrangeBookImportResult result,
            CancellationToken token,
            Action<string>? reportProgress = null)
        {
            #region implementation
            var (existingPairs, substanceLookup, matchedPairs) =
                await loadIngredientMatchingStateAsync(productIngredientMap, context, token);

            var newJunctions = new List<OrangeBook.ProductIngredientSubstance>();

            // Run tiers in priority order (phonetic tier removed: SOUNDEX produces too many
            // false positives for drug names, e.g., "metformin" matching "methadone")
            matchIngredientsByExactName(productIngredientMap, matchedPairs, substanceLookup,
                existingPairs, newJunctions, reportProgress);
            await matchIngredientsBySubstringAsync(productIngredientMap, matchedPairs,
                existingPairs, newJunctions, context, token, reportProgress);

            await persistIngredientJunctionsAsync(newJunctions, context, result, token);
            logUnmatchedIngredients(productIngredientMap, matchedPairs, result);
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Loads the current ingredient matching state from the database: existing junction
        /// pairs, a substance name lookup, and pre-matched (productId, ingredientName) pairs.
        /// </summary>
        /// <param name="productIngredientMap">Product-to-ingredients map for reverse-lookup seeding.</param>
        /// <param name="context">The database context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tuple of existing pairs, substance name lookup, and pre-matched pairs.</returns>
        /// <seealso cref="OrangeBook.ProductIngredientSubstance"/>
        /// <seealso cref="Label.IngredientSubstance"/>
        private async Task<(
            HashSet<(int, int)> existingPairs,
            Dictionary<string, List<int>> substanceLookup,
            HashSet<(int, string)> matchedPairs)>
            loadIngredientMatchingStateAsync(
                Dictionary<int, List<string>> productIngredientMap,
                ApplicationDbContext context,
                CancellationToken token)
        {
            #region implementation
            // Load existing junction rows
            var existingJunctions = await context.Set<OrangeBook.ProductIngredientSubstance>()
                .Select(j => new { j.OrangeBookProductID, j.IngredientSubstanceID })
                .ToListAsync(token);

            var existingPairs = new HashSet<(int, int)>(
                existingJunctions
                    .Where(j => j.OrangeBookProductID.HasValue && j.IngredientSubstanceID.HasValue)
                    .Select(j => (j.OrangeBookProductID!.Value, j.IngredientSubstanceID!.Value)));

            // Load all substance entities and build a name → IDs lookup
            var substances = await context.Set<Label.IngredientSubstance>()
                .Where(s => s.IngredientSubstanceID != null && s.SubstanceName != null)
                .Select(s => new { s.IngredientSubstanceID, s.SubstanceName })
                .ToListAsync(token);

            var substanceLookup = substances
                .GroupBy(s => s.SubstanceName!.ToUpper())
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(s => s.IngredientSubstanceID!.Value).ToList(),
                    StringComparer.OrdinalIgnoreCase);

            // Build reverse lookup: substanceId → upper-cased name for pre-seeding
            var substanceIdToName = substances
                .ToDictionary(s => s.IngredientSubstanceID!.Value, s => s.SubstanceName!.ToUpper());

            // Pre-seed matched pairs from existing junctions
            var matchedPairs = new HashSet<(int, string)>();
            foreach (var pair in existingPairs)
            {
                if (productIngredientMap.TryGetValue(pair.Item1, out var ingredientNames)
                    && substanceIdToName.TryGetValue(pair.Item2, out var substanceName))
                {
                    // Mark any ingredient name that matches this substance name
                    foreach (var name in ingredientNames)
                    {
                        if (name.Trim().ToUpper() == substanceName)
                        {
                            matchedPairs.Add((pair.Item1, name.Trim().ToUpper()));
                        }
                    }
                }
            }

            return (existingPairs, substanceLookup, matchedPairs);
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Filters the product-ingredient map to return (productId, ingredientName) tuples
        /// that have not yet been matched and meet the specified predicate.
        /// </summary>
        /// <param name="productIngredientMap">Product-to-ingredients map.</param>
        /// <param name="matchedPairs">Set of already-matched (productId, ingredientNameUpper) pairs.</param>
        /// <param name="predicate">Additional filter on the ingredient name string.</param>
        /// <returns>List of unmatched (productId, ingredientNameUpper) tuples.</returns>
        /// <seealso cref="OrangeBook.ProductIngredientSubstance"/>
        private List<(int productId, string ingredientNameUpper)> getUnmatchedProductIngredients(
            Dictionary<int, List<string>> productIngredientMap,
            HashSet<(int, string)> matchedPairs,
            Func<string, bool> predicate)
        {
            #region implementation
            var unmatched = new List<(int, string)>();

            foreach (var (productId, ingredientNames) in productIngredientMap)
            {
                foreach (var name in ingredientNames)
                {
                    var upper = name.Trim().ToUpper();
                    if (!matchedPairs.Contains((productId, upper)) && predicate(upper))
                    {
                        unmatched.Add((productId, upper));
                    }
                }
            }

            return unmatched;
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Tier 1: Exact case-insensitive match of ingredient names against
        /// <see cref="Label.IngredientSubstance.SubstanceName"/> using the pre-built
        /// in-memory lookup. No database query required.
        /// </summary>
        /// <param name="productIngredientMap">Product-to-ingredients map.</param>
        /// <param name="matchedPairs">Set of matched pairs (mutated on match).</param>
        /// <param name="substanceLookup">Upper-cased SubstanceName → List of IngredientSubstanceID.</param>
        /// <param name="existingPairs">Set of existing junction pairs (mutated on new junction).</param>
        /// <param name="newJunctions">List of new junction entities to insert (mutated).</param>
        /// <param name="reportProgress">Optional progress callback.</param>
        /// <seealso cref="Label.IngredientSubstance.SubstanceName"/>
        private void matchIngredientsByExactName(
            Dictionary<int, List<string>> productIngredientMap,
            HashSet<(int, string)> matchedPairs,
            Dictionary<string, List<int>> substanceLookup,
            HashSet<(int, int)> existingPairs,
            List<OrangeBook.ProductIngredientSubstance> newJunctions,
            Action<string>? reportProgress = null)
        {
            #region implementation
            var candidates = getUnmatchedProductIngredients(productIngredientMap, matchedPairs, _ => true);

            foreach (var (productId, ingredientNameUpper) in candidates)
            {
                if (substanceLookup.TryGetValue(ingredientNameUpper, out var substanceIds))
                {
                    foreach (var substanceId in substanceIds)
                    {
                        addIngredientJunctionIfNew(productId, substanceId, existingPairs, newJunctions);
                    }
                    matchedPairs.Add((productId, ingredientNameUpper));
                }
            }

            reportProgress?.Invoke($"Ingredients (exact): {newJunctions.Count} matches from {candidates.Count} pairs");
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Tier 2: Substring match where <see cref="Label.IngredientSubstance.SubstanceName"/>
        /// contains the ingredient name. Candidates are deduplicated by ingredient name before
        /// querying. Ingredient names under 3 characters are skipped to avoid false positives.
        /// </summary>
        /// <param name="productIngredientMap">Product-to-ingredients map.</param>
        /// <param name="matchedPairs">Set of matched pairs (mutated on match).</param>
        /// <param name="existingPairs">Set of existing junction pairs (mutated on new junction).</param>
        /// <param name="newJunctions">List of new junction entities to insert (mutated).</param>
        /// <param name="context">The database context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="reportProgress">Optional progress callback.</param>
        /// <seealso cref="Label.IngredientSubstance.SubstanceName"/>
        private async Task matchIngredientsBySubstringAsync(
            Dictionary<int, List<string>> productIngredientMap,
            HashSet<(int, string)> matchedPairs,
            HashSet<(int, int)> existingPairs,
            List<OrangeBook.ProductIngredientSubstance> newJunctions,
            ApplicationDbContext context,
            CancellationToken token,
            Action<string>? reportProgress = null)
        {
            #region implementation
            var candidates = getUnmatchedProductIngredients(productIngredientMap, matchedPairs,
                name => name.Length >= 3);

            // Deduplicate by ingredient name to avoid redundant queries
            var distinctNames = candidates.Select(c => c.ingredientNameUpper).Distinct().ToList();
            var substringResults = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            var queried = 0;
            var totalDistinct = distinctNames.Count;

            foreach (var ingredientName in distinctNames)
            {
                token.ThrowIfCancellationRequested();

                var matches = await context.Set<Label.IngredientSubstance>()
                    .Where(s => s.SubstanceName != null && s.IngredientSubstanceID != null
                        && s.SubstanceName.ToUpper().Contains(ingredientName))
                    .Select(s => s.IngredientSubstanceID!.Value)
                    .ToListAsync(token);

                if (matches.Count > 0)
                    substringResults[ingredientName] = matches;

                queried++;
                if (queried % 25 == 0 || queried == totalDistinct)
                    reportProgress?.Invoke($"Ingredients (substring): {queried}/{totalDistinct} names queried ({newJunctions.Count} matches)");
            }

            // Apply results to all product-ingredient pairs
            foreach (var (productId, ingredientNameUpper) in candidates)
            {
                if (substringResults.TryGetValue(ingredientNameUpper, out var substanceIds))
                {
                    foreach (var substanceId in substanceIds)
                    {
                        addIngredientJunctionIfNew(productId, substanceId, existingPairs, newJunctions);
                    }
                    matchedPairs.Add((productId, ingredientNameUpper));
                }
            }
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Adds a new <see cref="OrangeBook.ProductIngredientSubstance"/> junction row to the
        /// pending list if the pair does not already exist in the database or pending list.
        /// </summary>
        /// <param name="productId">The OrangeBookProductID.</param>
        /// <param name="substanceId">The IngredientSubstanceID from the SPL IngredientSubstance table.</param>
        /// <param name="existingPairs">Set of already-persisted (productId, substanceId) pairs.</param>
        /// <param name="newJunctions">List of new junction entities to be inserted.</param>
        /// <seealso cref="OrangeBook.ProductIngredientSubstance"/>
        private void addIngredientJunctionIfNew(
            int productId,
            int substanceId,
            HashSet<(int, int)> existingPairs,
            List<OrangeBook.ProductIngredientSubstance> newJunctions)
        {
            #region implementation
            var pair = (productId, substanceId);

            if (existingPairs.Contains(pair))
                return;

            existingPairs.Add(pair);
            newJunctions.Add(new OrangeBook.ProductIngredientSubstance
            {
                OrangeBookProductID = productId,
                IngredientSubstanceID = substanceId
            });
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Persists all accumulated ingredient junction rows to the database in a single batch.
        /// </summary>
        /// <param name="newJunctions">New junction entities to insert.</param>
        /// <param name="context">The database context.</param>
        /// <param name="result">Import result to track match count.</param>
        /// <param name="token">Cancellation token.</param>
        /// <seealso cref="OrangeBook.ProductIngredientSubstance"/>
        private async Task persistIngredientJunctionsAsync(
            List<OrangeBook.ProductIngredientSubstance> newJunctions,
            ApplicationDbContext context,
            OrangeBookImportResult result,
            CancellationToken token)
        {
            #region implementation
            if (newJunctions.Count == 0)
                return;

            context.Set<OrangeBook.ProductIngredientSubstance>().AddRange(newJunctions);
            await context.SaveChangesAsync(token);
            result.IngredientSubstanceMatchesCreated = newJunctions.Count;
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Logs ingredient names that could not be matched to any IngredientSubstance record
        /// and updates the result count for manual review.
        /// </summary>
        /// <param name="productIngredientMap">Product-to-ingredients map.</param>
        /// <param name="matchedPairs">Set of matched (productId, ingredientNameUpper) pairs after all tiers.</param>
        /// <param name="result">Import result to track unmatched count.</param>
        /// <seealso cref="Label.IngredientSubstance"/>
        private void logUnmatchedIngredients(
            Dictionary<int, List<string>> productIngredientMap,
            HashSet<(int, string)> matchedPairs,
            OrangeBookImportResult result)
        {
            #region implementation
            var unmatchedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (productId, ingredientNames) in productIngredientMap)
            {
                foreach (var name in ingredientNames)
                {
                    var upper = name.Trim().ToUpper();
                    if (!matchedPairs.Contains((productId, upper)))
                    {
                        unmatchedNames.Add(upper);
                    }
                }
            }

            result.UnmatchedIngredients = unmatchedNames.Count;

            foreach (var name in unmatchedNames)
            {
                _logger.LogWarning("No IngredientSubstance match found for ingredient '{IngredientName}'", name);
            }
            #endregion
        }

        #endregion

        #region marketing category matching

        /*************************************************************/
        /// <summary>
        /// Builds a mapping of OrangeBookProductID to the full application number string
        /// (e.g., "NDA020610") constructed from the ApplType and ApplNo columns.
        /// </summary>
        /// <param name="dataRows">Parsed data rows from products.txt.</param>
        /// <param name="productIdMap">Dictionary mapping (ApplType, ApplNo, ProductNo) to OrangeBookProductID.</param>
        /// <returns>Dictionary mapping OrangeBookProductID to the full application number string.</returns>
        /// <seealso cref="mapApplTypeToPrefix"/>
        /// <seealso cref="Label.MarketingCategory.ApplicationOrMonographIDValue"/>
        private Dictionary<int, string> buildProductAppNumberMap(
            List<string[]> dataRows,
            Dictionary<(string, string, string), int> productIdMap)
        {
            #region implementation
            var map = new Dictionary<int, string>();

            foreach (var row in dataRows)
            {
                var applType = row[COL_APPL_TYPE]?.Trim() ?? string.Empty;
                var applNo = row[COL_APPL_NO]?.Trim() ?? string.Empty;
                var productNo = row[COL_PRODUCT_NO]?.Trim() ?? string.Empty;
                var naturalKey = (applType, applNo, productNo);

                if (!productIdMap.TryGetValue(naturalKey, out var productId))
                    continue;

                if (map.ContainsKey(productId))
                    continue;

                var prefix = mapApplTypeToPrefix(applType);
                var fullAppNumber = prefix + applNo;

                if (!string.IsNullOrWhiteSpace(fullAppNumber))
                    map[productId] = fullAppNumber;
            }

            return map;
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Orchestrates the tiered matching of Orange Book products to existing SPL
        /// <see cref="Label.MarketingCategory"/> records via application number. New matches
        /// are inserted into the <see cref="OrangeBook.ProductMarketingCategory"/> junction table.
        /// </summary>
        /// <param name="productAppNumberMap">Dictionary mapping OrangeBookProductID to full app number strings.</param>
        /// <param name="context">The database context for the current import scope.</param>
        /// <param name="result">Import result to track match counts.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="reportProgress">Optional progress callback.</param>
        /// <seealso cref="matchCategoriesByExactAppNumberAsync"/>
        /// <seealso cref="matchCategoriesBySubstringAsync"/>
        /// <seealso cref="OrangeBook.ProductMarketingCategory"/>
        private async Task matchProductsToMarketingCategoriesAsync(
            Dictionary<int, string> productAppNumberMap,
            ApplicationDbContext context,
            OrangeBookImportResult result,
            CancellationToken token,
            Action<string>? reportProgress = null)
        {
            #region implementation
            var (existingPairs, matchedProductIds) =
                await loadCategoryMatchingStateAsync(context, token);

            var newJunctions = new List<OrangeBook.ProductMarketingCategory>();

            // Run tiers in priority order (phonetic tier removed: SOUNDEX on alphanumeric
            // app numbers like "NDA020610" ignores digits, causing near-universal matches)
            await matchCategoriesByExactAppNumberAsync(productAppNumberMap, matchedProductIds,
                existingPairs, newJunctions, context, token, reportProgress);
            await matchCategoriesBySubstringAsync(productAppNumberMap, matchedProductIds,
                existingPairs, newJunctions, context, token, reportProgress);

            await persistCategoryJunctionsAsync(newJunctions, context, result, token);
            logUnmatchedProducts(productAppNumberMap, matchedProductIds, result);
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Loads the current marketing category matching state from the database: existing
        /// junction pairs and a set of product IDs that already have junctions.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tuple of existing pairs and pre-matched product IDs.</returns>
        /// <seealso cref="OrangeBook.ProductMarketingCategory"/>
        private async Task<(HashSet<(int, int)> existingPairs, HashSet<int> matchedProductIds)>
            loadCategoryMatchingStateAsync(ApplicationDbContext context, CancellationToken token)
        {
            #region implementation
            var existingJunctions = await context.Set<OrangeBook.ProductMarketingCategory>()
                .Select(j => new { j.OrangeBookProductID, j.MarketingCategoryID })
                .ToListAsync(token);

            var existingPairs = new HashSet<(int, int)>(
                existingJunctions
                    .Where(j => j.OrangeBookProductID.HasValue && j.MarketingCategoryID.HasValue)
                    .Select(j => (j.OrangeBookProductID!.Value, j.MarketingCategoryID!.Value)));

            var matchedProductIds = new HashSet<int>(existingPairs.Select(p => p.Item1));

            return (existingPairs, matchedProductIds);
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Returns product IDs from the app number map that are not yet in the matched set.
        /// </summary>
        /// <param name="productAppNumberMap">Product-to-app-number map.</param>
        /// <param name="matchedProductIds">Set of already-matched product IDs.</param>
        /// <returns>List of unmatched (productId, appNumber) tuples.</returns>
        /// <seealso cref="OrangeBook.ProductMarketingCategory"/>
        private List<(int productId, string appNumber)> getUnmatchedProducts(
            Dictionary<int, string> productAppNumberMap,
            HashSet<int> matchedProductIds)
        {
            #region implementation
            return productAppNumberMap
                .Where(kvp => !matchedProductIds.Contains(kvp.Key))
                .Select(kvp => (kvp.Key, kvp.Value))
                .ToList();
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Tier 1: Exact match of the full application number (e.g., "NDA020610") and a
        /// fallback on just the numeric ApplNo against
        /// <see cref="Label.MarketingCategory.ApplicationOrMonographIDValue"/>.
        /// Uses a batch query for efficient matching.
        /// </summary>
        /// <param name="productAppNumberMap">Product-to-app-number map.</param>
        /// <param name="matchedProductIds">Set of matched product IDs (mutated on match).</param>
        /// <param name="existingPairs">Set of existing junction pairs (mutated on new junction).</param>
        /// <param name="newJunctions">List of new junction entities to insert (mutated).</param>
        /// <param name="context">The database context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="reportProgress">Optional progress callback.</param>
        /// <seealso cref="Label.MarketingCategory.ApplicationOrMonographIDValue"/>
        private async Task matchCategoriesByExactAppNumberAsync(
            Dictionary<int, string> productAppNumberMap,
            HashSet<int> matchedProductIds,
            HashSet<(int, int)> existingPairs,
            List<OrangeBook.ProductMarketingCategory> newJunctions,
            ApplicationDbContext context,
            CancellationToken token,
            Action<string>? reportProgress = null)
        {
            #region implementation
            var candidates = getUnmatchedProducts(productAppNumberMap, matchedProductIds);
            if (candidates.Count == 0)
                return;

            // Batch query: collect distinct app numbers (full prefix + number, e.g., "NDA020610")
            var fullAppNumbers = candidates
                .Select(c => c.appNumber.ToUpper())
                .Distinct()
                .ToList();

            var exactMatchCategories = await context.Set<Label.MarketingCategory>()
                .Where(mc => mc.ApplicationOrMonographIDValue != null && mc.MarketingCategoryID != null
                    && fullAppNumbers.Contains(mc.ApplicationOrMonographIDValue.ToUpper()))
                .Select(mc => new { mc.MarketingCategoryID, mc.ApplicationOrMonographIDValue })
                .ToListAsync(token);

            // Build lookup: upper-cased app number → list of category IDs
            var appNumberLookup = exactMatchCategories
                .Where(mc => mc.ApplicationOrMonographIDValue != null && mc.MarketingCategoryID.HasValue)
                .GroupBy(mc => mc.ApplicationOrMonographIDValue!.ToUpper())
                .ToDictionary(g => g.Key, g => g.Select(mc => mc.MarketingCategoryID!.Value).ToList());

            foreach (var (productId, appNumber) in candidates)
            {
                if (appNumberLookup.TryGetValue(appNumber.ToUpper(), out var categoryIds))
                {
                    foreach (var categoryId in categoryIds)
                    {
                        addCategoryJunctionIfNew(productId, categoryId, existingPairs, newJunctions);
                    }
                    matchedProductIds.Add(productId);
                }
            }

            // Fallback: retry unmatched products using just the numeric portion (ApplNo)
            var stillUnmatched = getUnmatchedProducts(productAppNumberMap, matchedProductIds);
            if (stillUnmatched.Count == 0)
                return;

            // Extract numeric portion by stripping the prefix
            var numericAppNumbers = stillUnmatched
                .Select(c =>
                {
                    var upper = c.appNumber.ToUpper();
                    if (upper.StartsWith("NDA")) return upper.Substring(3);
                    if (upper.StartsWith("ANDA")) return upper.Substring(4);
                    return upper;
                })
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .ToList();

            var numericMatchCategories = await context.Set<Label.MarketingCategory>()
                .Where(mc => mc.ApplicationOrMonographIDValue != null && mc.MarketingCategoryID != null
                    && numericAppNumbers.Contains(mc.ApplicationOrMonographIDValue.ToUpper()))
                .Select(mc => new { mc.MarketingCategoryID, mc.ApplicationOrMonographIDValue })
                .ToListAsync(token);

            var numericLookup = numericMatchCategories
                .Where(mc => mc.ApplicationOrMonographIDValue != null && mc.MarketingCategoryID.HasValue)
                .GroupBy(mc => mc.ApplicationOrMonographIDValue!.ToUpper())
                .ToDictionary(g => g.Key, g => g.Select(mc => mc.MarketingCategoryID!.Value).ToList());

            foreach (var (productId, appNumber) in stillUnmatched)
            {
                var upper = appNumber.ToUpper();
                string numeric;
                if (upper.StartsWith("NDA")) numeric = upper.Substring(3);
                else if (upper.StartsWith("ANDA")) numeric = upper.Substring(4);
                else numeric = upper;

                if (numericLookup.TryGetValue(numeric, out var categoryIds))
                {
                    foreach (var categoryId in categoryIds)
                    {
                        addCategoryJunctionIfNew(productId, categoryId, existingPairs, newJunctions);
                    }
                    matchedProductIds.Add(productId);
                }
            }

            reportProgress?.Invoke($"Categories (exact): {newJunctions.Count} matches from {candidates.Count} products");
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Tier 2: Substring match where <see cref="Label.MarketingCategory.ApplicationOrMonographIDValue"/>
        /// contains the numeric application number. Candidates are deduplicated by numeric portion
        /// before querying to avoid redundant database calls. Application numbers under 3
        /// characters are skipped.
        /// </summary>
        /// <param name="productAppNumberMap">Product-to-app-number map.</param>
        /// <param name="matchedProductIds">Set of matched product IDs (mutated on match).</param>
        /// <param name="existingPairs">Set of existing junction pairs (mutated on new junction).</param>
        /// <param name="newJunctions">List of new junction entities to insert (mutated).</param>
        /// <param name="context">The database context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="reportProgress">Optional progress callback.</param>
        /// <seealso cref="Label.MarketingCategory.ApplicationOrMonographIDValue"/>
        private async Task matchCategoriesBySubstringAsync(
            Dictionary<int, string> productAppNumberMap,
            HashSet<int> matchedProductIds,
            HashSet<(int, int)> existingPairs,
            List<OrangeBook.ProductMarketingCategory> newJunctions,
            ApplicationDbContext context,
            CancellationToken token,
            Action<string>? reportProgress = null)
        {
            #region implementation
            var candidates = getUnmatchedProducts(productAppNumberMap, matchedProductIds);

            // Extract the numeric portion of each candidate's app number
            var candidatesWithNumeric = candidates
                .Select(c =>
                {
                    var upper = c.appNumber.ToUpper();
                    string numeric;
                    if (upper.StartsWith("NDA")) numeric = upper.Substring(3);
                    else if (upper.StartsWith("ANDA")) numeric = upper.Substring(4);
                    else numeric = upper;
                    return (c.productId, c.appNumber, numeric);
                })
                .Where(c => c.numeric.Length >= 3)
                .ToList();

            // Deduplicate by numeric portion to avoid redundant queries
            var distinctNumerics = candidatesWithNumeric
                .Select(c => c.numeric)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var substringResults = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            var queried = 0;
            var totalDistinct = distinctNumerics.Count;

            foreach (var numeric in distinctNumerics)
            {
                token.ThrowIfCancellationRequested();

                var matches = await context.Set<Label.MarketingCategory>()
                    .Where(mc => mc.ApplicationOrMonographIDValue != null && mc.MarketingCategoryID != null
                        && mc.ApplicationOrMonographIDValue.ToUpper().Contains(numeric))
                    .Select(mc => mc.MarketingCategoryID!.Value)
                    .ToListAsync(token);

                if (matches.Count > 0)
                    substringResults[numeric] = matches;

                queried++;
                if (queried % 25 == 0 || queried == totalDistinct)
                    reportProgress?.Invoke($"Categories (substring): {queried}/{totalDistinct} numbers queried ({newJunctions.Count} matches)");
            }

            // Apply cached results to all product-app-number pairs
            foreach (var (productId, appNumber, numeric) in candidatesWithNumeric)
            {
                if (substringResults.TryGetValue(numeric, out var categoryIds))
                {
                    foreach (var categoryId in categoryIds)
                    {
                        addCategoryJunctionIfNew(productId, categoryId, existingPairs, newJunctions);
                    }
                    matchedProductIds.Add(productId);
                }
            }
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Adds a new <see cref="OrangeBook.ProductMarketingCategory"/> junction row to the
        /// pending list if the pair does not already exist in the database or pending list.
        /// </summary>
        /// <param name="productId">The OrangeBookProductID.</param>
        /// <param name="categoryId">The MarketingCategoryID from the SPL MarketingCategory table.</param>
        /// <param name="existingPairs">Set of already-persisted (productId, categoryId) pairs.</param>
        /// <param name="newJunctions">List of new junction entities to be inserted.</param>
        /// <seealso cref="OrangeBook.ProductMarketingCategory"/>
        private void addCategoryJunctionIfNew(
            int productId,
            int categoryId,
            HashSet<(int, int)> existingPairs,
            List<OrangeBook.ProductMarketingCategory> newJunctions)
        {
            #region implementation
            var pair = (productId, categoryId);

            if (existingPairs.Contains(pair))
                return;

            existingPairs.Add(pair);
            newJunctions.Add(new OrangeBook.ProductMarketingCategory
            {
                OrangeBookProductID = productId,
                MarketingCategoryID = categoryId
            });
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Persists all accumulated marketing category junction rows to the database in a single batch.
        /// </summary>
        /// <param name="newJunctions">New junction entities to insert.</param>
        /// <param name="context">The database context.</param>
        /// <param name="result">Import result to track match count.</param>
        /// <param name="token">Cancellation token.</param>
        /// <seealso cref="OrangeBook.ProductMarketingCategory"/>
        private async Task persistCategoryJunctionsAsync(
            List<OrangeBook.ProductMarketingCategory> newJunctions,
            ApplicationDbContext context,
            OrangeBookImportResult result,
            CancellationToken token)
        {
            #region implementation
            if (newJunctions.Count == 0)
                return;

            context.Set<OrangeBook.ProductMarketingCategory>().AddRange(newJunctions);
            await context.SaveChangesAsync(token);
            result.MarketingCategoryMatchesCreated = newJunctions.Count;
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Logs products that could not be matched to any MarketingCategory record
        /// and updates the result count for manual review.
        /// </summary>
        /// <param name="productAppNumberMap">Product-to-app-number map.</param>
        /// <param name="matchedProductIds">Set of matched product IDs after all tiers.</param>
        /// <param name="result">Import result to track unmatched count.</param>
        /// <seealso cref="Label.MarketingCategory"/>
        private void logUnmatchedProducts(
            Dictionary<int, string> productAppNumberMap,
            HashSet<int> matchedProductIds,
            OrangeBookImportResult result)
        {
            #region implementation
            var unmatched = productAppNumberMap
                .Where(kvp => !matchedProductIds.Contains(kvp.Key))
                .ToList();

            result.UnmatchedProducts = unmatched.Count;

            foreach (var (productId, appNumber) in unmatched)
            {
                _logger.LogWarning("No MarketingCategory match found for product {ProductId} (app number: '{AppNumber}')",
                    productId, appNumber);
            }
            #endregion
        }

        #endregion
    }

    /*************************************************************/
    /// <summary>
    /// Result of an Orange Book import operation, tracking counts of created, updated,
    /// and unmatched entities across all import phases (products, patents, matching)
    /// along with any errors encountered.
    /// </summary>
    /// <seealso cref="OrangeBookProductParsingService"/>
    /// <seealso cref="OrangeBookPatentParsingService"/>
    /// <seealso cref="OrangeBook.Product"/>
    /// <seealso cref="OrangeBook.Patent"/>
    /// <seealso cref="OrangeBook.Applicant"/>
    /// <seealso cref="OrangeBook.ApplicantOrganization"/>
    /// <seealso cref="OrangeBook.ProductIngredientSubstance"/>
    /// <seealso cref="OrangeBook.ProductMarketingCategory"/>
    public class OrangeBookImportResult
    {
        #region properties

        /*************************************************************/
        /// <summary>
        /// Indicates whether the import completed without critical errors.
        /// </summary>
        public bool Success { get; set; } = true;

        /*************************************************************/
        /// <summary>
        /// Number of new applicant records inserted into OrangeBookApplicant.
        /// </summary>
        /// <seealso cref="OrangeBook.Applicant"/>
        public int ApplicantsCreated { get; set; }

        /*************************************************************/
        /// <summary>
        /// Number of existing applicant records updated (e.g., full name changed).
        /// </summary>
        /// <seealso cref="OrangeBook.Applicant"/>
        public int ApplicantsUpdated { get; set; }

        /*************************************************************/
        /// <summary>
        /// Number of new product records inserted into OrangeBookProduct.
        /// </summary>
        /// <seealso cref="OrangeBook.Product"/>
        public int ProductsCreated { get; set; }

        /*************************************************************/
        /// <summary>
        /// Number of existing product records updated with new field values.
        /// </summary>
        /// <seealso cref="OrangeBook.Product"/>
        public int ProductsUpdated { get; set; }

        /*************************************************************/
        /// <summary>
        /// Number of new patent records inserted into OrangeBookPatent.
        /// </summary>
        /// <seealso cref="OrangeBook.Patent"/>
        public int PatentsCreated { get; set; }

        /*************************************************************/
        /// <summary>
        /// Number of existing patent records updated with new field values.
        /// </summary>
        /// <seealso cref="OrangeBook.Patent"/>
        public int PatentsUpdated { get; set; }

        /*************************************************************/
        /// <summary>
        /// Number of patent records successfully linked to a parent product
        /// via the composite natural key (ApplType, ApplNo, ProductNo).
        /// </summary>
        /// <seealso cref="OrangeBook.Patent.OrangeBookProductID"/>
        /// <seealso cref="OrangeBook.Product"/>
        public int PatentsLinkedToProduct { get; set; }

        /*************************************************************/
        /// <summary>
        /// Number of patent records where no matching product was found for the
        /// natural key (ApplType, ApplNo, ProductNo). These patents have
        /// OrangeBookProductID set to null.
        /// </summary>
        /// <seealso cref="OrangeBook.Patent.OrangeBookProductID"/>
        public int UnlinkedPatents { get; set; }

        /*************************************************************/
        /// <summary>
        /// Number of new ApplicantOrganization junction rows created.
        /// </summary>
        /// <seealso cref="OrangeBook.ApplicantOrganization"/>
        public int OrganizationMatchesCreated { get; set; }

        /*************************************************************/
        /// <summary>
        /// Number of data rows skipped due to incorrect column count.
        /// </summary>
        public int MalformedRowsSkipped { get; set; }

        /*************************************************************/
        /// <summary>
        /// Number of applicants that could not be matched to any Organization record.
        /// </summary>
        public int UnmatchedApplicants { get; set; }

        /*************************************************************/
        /// <summary>
        /// Number of new ProductIngredientSubstance junction rows created.
        /// </summary>
        /// <seealso cref="OrangeBook.ProductIngredientSubstance"/>
        public int IngredientSubstanceMatchesCreated { get; set; }

        /*************************************************************/
        /// <summary>
        /// Number of distinct ingredient names (from semicolon-delimited Product.Ingredient)
        /// that could not be matched to any IngredientSubstance record after all matching tiers.
        /// </summary>
        public int UnmatchedIngredients { get; set; }

        /*************************************************************/
        /// <summary>
        /// Number of new ProductMarketingCategory junction rows created.
        /// </summary>
        /// <seealso cref="OrangeBook.ProductMarketingCategory"/>
        public int MarketingCategoryMatchesCreated { get; set; }

        /*************************************************************/
        /// <summary>
        /// Number of products whose application number could not be matched to any
        /// MarketingCategory record after all matching tiers.
        /// </summary>
        public int UnmatchedProducts { get; set; }

        /*************************************************************/
        /// <summary>
        /// List of error messages encountered during import.
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /*************************************************************/
        /// <summary>
        /// Summary message describing the overall import outcome.
        /// </summary>
        public string? Message { get; set; }

        #endregion
    }
}
