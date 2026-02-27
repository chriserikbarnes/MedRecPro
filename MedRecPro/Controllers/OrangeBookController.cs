using MedRecPro.Controllers;
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using static MedRecPro.Models.LabelView;

namespace MedRecPro.Api.Controllers
{
    /**************************************************************/
    /// <summary>
    /// API controller for Orange Book patent data discovery.
    /// Provides patent expiration tracking for NDA products with
    /// cross-references to SPL drug labels.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/> for data retrieval.
    /// Applies pediatric deduplication when <c>*PED</c> companion patents exist,
    /// and generates pre-rendered markdown tables for display.
    ///
    /// ## Pediatric Handling
    /// When both a base patent and its <c>*PED</c> companion appear in results,
    /// the base row is filtered out and only the <c>*PED</c> row (with the extended
    /// pediatric exclusivity expiration date) is retained.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>
    /// <seealso cref="OrangeBookPatentDto"/>
    /// <seealso cref="LabelView.OrangeBookPatent"/>
    [ApiController]
    public class OrangeBookController : ApiControllerBase
    {
        #region Private Properties

        /**************************************************************/
        /// <summary>
        /// Configuration provider for accessing application settings.
        /// </summary>
        private readonly IConfiguration _configuration;

        /**************************************************************/
        /// <summary>
        /// Logger instance for this controller.
        /// </summary>
        private readonly ILogger<OrangeBookController> _logger;

        /**************************************************************/
        /// <summary>
        /// String cipher utility for encrypting and decrypting primary keys.
        /// </summary>
        private readonly StringCipher _stringCipher;

        /**************************************************************/
        /// <summary>
        /// Database context for Entity Framework Core queries.
        /// </summary>
        private readonly ApplicationDbContext _dbContext;

        /**************************************************************/
        /// <summary>
        /// Secret key used for primary key encryption, retrieved from configuration.
        /// </summary>
        private readonly string _pkEncryptionSecret;

        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="OrangeBookController"/> with required dependencies.
        /// </summary>
        /// <param name="configuration">Configuration provider for application settings.</param>
        /// <param name="logger">Logger instance for this controller.</param>
        /// <param name="stringCipher">String cipher utility for encryption operations.</param>
        /// <param name="applicationDbContext">Database context for data access.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when PKSecret configuration is missing.</exception>
        public OrangeBookController(
            IConfiguration configuration,
            ILogger<OrangeBookController> logger,
            StringCipher stringCipher,
            ApplicationDbContext applicationDbContext)
        {
            #region implementation

            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stringCipher = stringCipher ?? throw new ArgumentNullException(nameof(stringCipher));
            _dbContext = applicationDbContext ?? throw new ArgumentNullException(nameof(applicationDbContext));

            // Retrieve and validate the primary key encryption secret from configuration
            _pkEncryptionSecret = _configuration.GetSection("Security:DB:PKSecret").Value
                ?? throw new InvalidOperationException(
                    "Configuration key 'Security:DB:PKSecret' is missing or empty.");

            #endregion
        }

        #region Public Methods

        /**************************************************************/
        /// <summary>
        /// Discovers NDA patents expiring within a specified time horizon.
        /// Returns patent data with pediatric deduplication and a pre-rendered markdown table.
        /// </summary>
        /// <param name="expiringInMonths">
        /// Number of months from today to search for expiring patents.
        /// Must be greater than 0. Example: 6 returns patents expiring between today and 6 months from now.
        /// </param>
        /// <param name="pageNumber">Page number (1-based). Defaults to 1 when pageSize is provided.</param>
        /// <param name="pageSize">Results per page. Defaults to 10 when pageNumber is provided.</param>
        /// <returns>
        /// JSON response containing:
        /// - **Patents**: filtered list of matching patent DTOs (base rows removed when *PED companion exists)
        /// - **Markdown**: pre-rendered markdown table with pediatric warnings and label links
        /// - **TotalCount**: count of results in the current page after pediatric deduplication
        /// </returns>
        /// <remarks>
        /// ## Pediatric Deduplication
        /// When a patent has a `*PED` companion (pediatric exclusivity), only the `*PED` row
        /// is returned — it carries the extended expiration date. The base patent row is filtered
        /// out to prevent duplication. Rows with pediatric exclusivity are marked with a
        /// warning emoji (&#x26A0;&#xFE0F;) in the markdown table.
        ///
        /// ## Markdown Table Columns
        /// | Type | Application# | Prod# | Trade Name | Strength | Patent# | Expires |
        ///
        /// Trade Name includes lowercase ingredient in italics. When a DocumentGUID cross-reference
        /// is available, the Trade Name is a markdown link to the original FDA label.
        ///
        /// ## Pagination
        /// Response headers include X-Page-Number, X-Page-Size, and X-Total-Count when pagination is applied.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/OrangeBook/expiring?expiringInMonths=6
        /// GET /api/OrangeBook/expiring?expiringInMonths=12&amp;pageNumber=2&amp;pageSize=25
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>
        /// <seealso cref="OrangeBookPatentExpirationResponseDto"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [HttpGet("expiring")]
        [ProducesResponseType(typeof(OrangeBookPatentExpirationResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OrangeBookPatentExpirationResponseDto>> GetExpiringPatents(
            [FromQuery] int expiringInMonths,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region implementation

            // Validate expiringInMonths
            if (expiringInMonths <= 0)
            {
                return BadRequest("ExpiringInMonths must be greater than 0.");
            }

            // Validate pagination parameters (inherited from ApiControllerBase)
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            // Enforce pagination defaults — this endpoint always paginates
            pageNumber ??= DefaultPageNumber;
            pageSize ??= DefaultPageSize;

            try
            {
                _logger.LogInformation(
                    "Searching expiring patents: months={Months}, page={Page}, size={Size}",
                    expiringInMonths, pageNumber, pageSize);

                // 1. Get the total count of matching patents (before pagination)
                var totalCount = await countExpiringPatentsAsync(expiringInMonths);

                // 2. Retrieve paginated patent data (includes both base and *PED rows)
                var rawPatents = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
                    _dbContext,
                    expiringInMonths: expiringInMonths,
                    documentGuid: null,
                    applicationNumber: null,
                    ingredient: null,
                    tradeName: null,
                    patentNo: null,
                    patentExpireDate: null,
                    hasPediatricFlag: null,
                    hasWithdrawnCommercialReasonFlag: null,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // 3. Filter out base patent rows when their *PED companion is present
                var filteredPatents = filterPediatricDuplicates(rawPatents);

                // 4. Resolve absolute URLs for MCP consumers that need full paths
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                resolveAbsoluteLabelLinks(filteredPatents, baseUrl);

                // 5. Generate pre-rendered markdown table from filtered results
                var markdown = generatePatentExpirationMarkdown(filteredPatents, baseUrl);

                // 6. Compute total pages and add pagination headers
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize.Value);
                addPaginationHeaders(pageNumber, pageSize, totalCount);

                // 7. Build JSON response
                var response = new OrangeBookPatentExpirationResponseDto
                {
                    Patents = filteredPatents,
                    Markdown = markdown,
                    TotalCount = totalCount,
                    TotalPages = totalPages
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error searching expiring patents for {Months} months",
                    expiringInMonths);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while searching for expiring patents.");
            }

            #endregion
        }

        #endregion

        #region Private Methods

        /**************************************************************/
        /// <summary>
        /// Counts the total number of patents expiring within the specified month horizon.
        /// Used to compute <see cref="OrangeBookPatentExpirationResponseDto.TotalPages"/>.
        /// </summary>
        /// <param name="expiringInMonths">Number of months from today to search.</param>
        /// <returns>Total count of matching patent records in the view.</returns>
        /// <remarks>
        /// Runs a lightweight COUNT(*) query against vw_OrangeBookPatent with the same
        /// date range filter used by <see cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>.
        /// </remarks>
        private async Task<int> countExpiringPatentsAsync(int expiringInMonths)
        {
            #region implementation

            var rangeStart = DateTime.Today;
            var rangeEnd = DateTime.Today.AddMonths(expiringInMonths);

            return await _dbContext.Set<OrangeBookPatent>()
                .AsNoTracking()
                .Where(p => p.PatentExpireDate >= rangeStart && p.PatentExpireDate <= rangeEnd)
                .CountAsync();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Filters out base patent rows when their <c>*PED</c> companion is present
        /// in the result set, retaining only the pediatric exclusivity row.
        /// </summary>
        /// <param name="patents">Raw patent DTOs from the data access layer.</param>
        /// <returns>
        /// Filtered list where base patents superseded by a <c>*PED</c> companion
        /// are removed. Non-pediatric patents are passed through unchanged.
        /// </returns>
        /// <remarks>
        /// When pagination splits a base patent and its <c>*PED</c> companion across pages,
        /// the base row is kept (since the companion is not available for deduplication)
        /// and still displays with the pediatric warning emoji.
        /// </remarks>
        private static List<OrangeBookPatentDto> filterPediatricDuplicates(List<OrangeBookPatentDto> patents)
        {
            #region implementation

            if (patents.Count == 0)
            {
                return patents;
            }

            // Build a set of base patent numbers that have a *PED companion in this result set.
            // E.g., if "4681893*PED" exists, add "4681893" to the set.
            var baseNumbersWithPedCompanion = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var patent in patents)
            {
                var patentNo = patent.PatentNo;
                if (!string.IsNullOrEmpty(patentNo) && patentNo.EndsWith("*PED", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract the base patent number by removing the *PED suffix
                    var baseNumber = patentNo[..^4]; // Remove last 4 chars ("*PED")
                    baseNumbersWithPedCompanion.Add(baseNumber);
                }
            }

            // If no *PED rows were found, return the original list unchanged
            if (baseNumbersWithPedCompanion.Count == 0)
            {
                return patents;
            }

            // Filter out base rows whose *PED companion is present in the result set
            return patents.Where(p =>
            {
                var patentNo = p.PatentNo;
                if (string.IsNullOrEmpty(patentNo))
                {
                    return true; // Keep rows with null/empty patent numbers
                }

                // Keep *PED rows (they are the ones we want)
                if (patentNo.EndsWith("*PED", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Remove base rows that have a *PED companion in this result set
                return !baseNumbersWithPedCompanion.Contains(patentNo);
            }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves relative <see cref="OrangeBookPatentDto.LabelLink"/> values to absolute URLs.
        /// MCP tool consumers require full URLs since they cannot infer the server's scheme and host.
        /// </summary>
        /// <param name="patents">Patent DTOs whose LabelLink values will be updated in place.</param>
        /// <param name="baseUrl">Base URL including scheme and host (e.g., "https://medrec.pro").</param>
        private static void resolveAbsoluteLabelLinks(List<OrangeBookPatentDto> patents, string baseUrl)
        {
            #region implementation

            foreach (var patent in patents)
            {
                if (!string.IsNullOrEmpty(patent.LabelLink))
                {
                    patent.LabelLink = $"{baseUrl}{patent.LabelLink}";
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates a pre-rendered markdown table from the filtered patent list.
        /// </summary>
        /// <param name="patents">Filtered patent DTOs (after pediatric deduplication).</param>
        /// <param name="baseUrl">Base URL including scheme and host for building absolute label links.</param>
        /// <returns>
        /// Markdown table string with columns: Type, Application#, Prod#, Trade Name,
        /// Strength, Patent#, and Expires. Includes a footer legend for the pediatric
        /// warning emoji.
        /// </returns>
        /// <remarks>
        /// - Trade Name concatenates the brand name with lowercase ingredient in italics.
        /// - When DocumentGUID is available, Trade Name is an absolute markdown link to the original label.
        /// - Pediatric rows (HasPediatricFlag = true) show a warning emoji in the Expires column.
        /// - Pipe characters in data values are escaped to prevent markdown table corruption.
        /// </remarks>
        private static string generatePatentExpirationMarkdown(List<OrangeBookPatentDto> patents, string baseUrl)
        {
            #region implementation

            var sb = new StringBuilder();

            // Header row
            sb.AppendLine("| Type | Application# | Prod# | Trade Name | Strength | Patent# | Expires |");
            sb.AppendLine("|------|-------------|-------|------------|----------|---------|---------|");

            foreach (var patent in patents)
            {
                var dict = patent.OrangeBookPatent;

                // Extract values — use helper properties when available, dictionary for the rest
                var applicationNumber = escapeMarkdownPipe(patent.ApplicationNumber ?? "");
                var productNo = escapeMarkdownPipe(getDictString(dict, "ProductNo"));
                var tradeName = patent.TradeName ?? "";
                var ingredient = (patent.Ingredient ?? "").ToLowerInvariant();
                var strength = escapeMarkdownPipe(getDictString(dict, "Strength"));
                var patentNo = escapeMarkdownPipe(patent.PatentNo ?? "");
                var expireDate = patent.PatentExpireDate?.ToString("yyyy-MM-dd") ?? "N/A";
                var hasPediatricFlag = getDictBool(dict, "HasPediatricFlag");

                // Build Trade Name cell: absolute link when DocumentGUID available, with lowercase ingredient
                string tradeNameCell;
                if (patent.DocumentGUID.HasValue)
                {
                    var labelLink = $"{baseUrl}/api/Label/original/{patent.DocumentGUID.Value}/false";
                    tradeNameCell = $"[{escapeMarkdownPipe(tradeName)}]({labelLink}) *({escapeMarkdownPipe(ingredient)})*";
                }
                else
                {
                    tradeNameCell = $"{escapeMarkdownPipe(tradeName)} *({escapeMarkdownPipe(ingredient)})*";
                }

                // Append pediatric warning emoji to expires column
                var expiresCell = hasPediatricFlag
                    ? $"{expireDate} \u26a0\ufe0f"
                    : expireDate;

                sb.AppendLine($"| NDA | {applicationNumber} | {productNo} | {tradeNameCell} | {strength} | {patentNo} | {expiresCell} |");
            }

            // Footer legend for pediatric emoji
            if (patents.Any(p => getDictBool(p.OrangeBookPatent, "HasPediatricFlag")))
            {
                sb.AppendLine();
                sb.AppendLine("\u26a0\ufe0f = Pediatric Exclusivity Expiration");
            }

            return sb.ToString();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Safely retrieves a string value from the patent dictionary.
        /// </summary>
        /// <param name="dict">The OrangeBookPatent dictionary.</param>
        /// <param name="key">The dictionary key to look up.</param>
        /// <returns>The string value, or empty string if not found or null.</returns>
        private static string getDictString(Dictionary<string, object?> dict, string key)
        {
            #region implementation

            return dict.TryGetValue(key, out var value) && value is string s ? s : "";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Safely retrieves a boolean value from the patent dictionary.
        /// </summary>
        /// <param name="dict">The OrangeBookPatent dictionary.</param>
        /// <param name="key">The dictionary key to look up.</param>
        /// <returns>True if the value is a true boolean, false otherwise.</returns>
        private static bool getDictBool(Dictionary<string, object?> dict, string key)
        {
            #region implementation

            return dict.TryGetValue(key, out var value) && value is bool b && b;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Escapes pipe characters in a string to prevent markdown table corruption.
        /// </summary>
        /// <param name="value">The string value to escape.</param>
        /// <returns>The escaped string with <c>|</c> replaced by <c>\|</c>.</returns>
        private static string escapeMarkdownPipe(string value)
        {
            #region implementation

            return value.Replace("|", "\\|");

            #endregion
        }

        #endregion
    }
}
