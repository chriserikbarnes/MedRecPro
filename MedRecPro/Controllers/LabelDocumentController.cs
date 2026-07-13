using MedRecPro.Controllers;
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Mappers;
using MedRecPro.Models;
using MedRecPro.Models.Extensions;
using MedRecPro.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Reflection;
using System.Security.Claims;
using static MedRecPro.Models.UserRole;

namespace MedRecPro.Api.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Handles complete-label and SPL XML document endpoints while preserving the original Label route surface.
    /// </summary>
    /// <remarks>
    /// This controller owns document navigation, complete label retrieval, generated SPL XML, and original XML download/display endpoints.
    /// </remarks>
    /// <seealso cref="LabelFeatureControllerAttribute"/>
    /// <seealso cref="LabelController"/>
    [ApiController]
    [LabelFeatureController]
    [LabelFeatureSwaggerTag("Label Documents")]
    public class LabelDocumentController : ApiControllerBase
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Service provider used by complete-label repository lookups while the legacy dynamic seam remains in place.
        /// </summary>
        /// <seealso cref="Repository{T}"/>
        private readonly IServiceProvider _serviceProvider;

        /**************************************************************/
        /// <summary>
        /// Configuration provider for SPL export feature flags and encryption settings.
        /// </summary>
        /// <seealso cref="IConfiguration"/>
        private readonly IConfiguration _configuration;

        /**************************************************************/
        /// <summary>
        /// Logger instance for document endpoint diagnostics.
        /// </summary>
        /// <seealso cref="ILogger"/>
        private readonly ILogger<LabelDocumentController> _logger;

        /**************************************************************/
        /// <summary>
        /// Secret key used for primary-key encryption during DTO projection.
        /// </summary>
        /// <seealso cref="DtoTransform"/>
        private readonly string _pkEncryptionSecret;

        /**************************************************************/
        /// <summary>
        /// Entity Framework database context used by document navigation data-access operations.
        /// </summary>
        /// <seealso cref="ApplicationDbContext"/>
        private readonly ApplicationDbContext _dbContext;

        /**************************************************************/
        /// <summary>
        /// Service for generating rendered SPL XML documents.
        /// </summary>
        /// <seealso cref="ISplExportService"/>
        private readonly ISplExportService _splExportService;

        /**************************************************************/
        /// <summary>
        /// Service for retrieving original imported SPL XML payloads.
        /// </summary>
        /// <seealso cref="SplDataService"/>
        private readonly SplDataService _splDataService;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="LabelDocumentController"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider for complete-label repository resolution.</param>
        /// <param name="configuration">Configuration provider for feature flags and encryption settings.</param>
        /// <param name="logger">Logger instance for document endpoint diagnostics.</param>
        /// <param name="applicationDbContext">Entity Framework database context for document navigation read models.</param>
        /// <param name="splExportService">SPL export service for generated XML documents.</param>
        /// <param name="splDataService">SPL data service for original XML documents.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the primary-key encryption secret is missing.</exception>
        /// <seealso cref="LabelController"/>
        public LabelDocumentController(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<LabelDocumentController> logger,
            ApplicationDbContext applicationDbContext,
            ISplExportService splExportService,
            SplDataService splDataService)
        {
            #region implementation

            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = applicationDbContext ?? throw new ArgumentNullException(nameof(applicationDbContext));
            _splExportService = splExportService ?? throw new ArgumentNullException(nameof(splExportService));
            _splDataService = splDataService ?? throw new ArgumentNullException(nameof(splDataService));
            _pkEncryptionSecret = _configuration.GetSection("Security:DB:PKSecret").Value
                ?? throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' is missing or empty.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if the request is from a browser attempting to view the XML directly.
        /// </summary>
        /// <param name="request">The HTTP request to analyze.</param>
        /// <returns>True if request is from a browser for direct viewing, false otherwise.</returns>
        /// <remarks>
        /// Checks Accept header for text/html preference, indicating browser navigation.
        /// Download requests and API calls typically use application/xml or */*.
        /// </remarks>
        /// <seealso cref="GenerateXmlDocument"/>
        private bool isBrowserViewRequest(HttpRequest request)
        {
            #region implementation

            // Check if Accept header prefers HTML (browser navigation)
            var acceptHeader = request.Headers["Accept"].ToString();

            // Browser direct navigation typically includes text/html in Accept header
            // Swagger downloads and validation tools use application/xml or */*
            if (acceptHeader.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Additional check: query string parameter for explicit control
            if (request.Query.ContainsKey("view") &&
                request.Query["view"].ToString().Equals("browser", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Ensures the XML declaration specifies UTF-8 encoding as required by FDA specification.
        /// </summary>
        /// <param name="xmlContent">The XML content to process.</param>
        /// <returns>XML content with corrected encoding declaration.</returns>
        /// <remarks>
        /// FDA SPL specification 2.1.2.1 requires UTF-8 encoding.
        /// Replaces any other encoding declarations (e.g., UTF-16) with UTF-8.
        /// </remarks>
        /// <seealso cref="GenerateXmlDocument"/>
        private string ensureUtf8Encoding(string xmlContent)
        {
            #region implementation

            if (!string.IsNullOrWhiteSpace(xmlContent))
            {
                // Replace any encoding declaration with UTF-8
                if (xmlContent.Contains("encoding=\"UTF-16\"", StringComparison.OrdinalIgnoreCase))
                {
                    xmlContent = xmlContent.Replace(
                        "encoding=\"UTF-16\"",
                        "encoding=\"UTF-8\"",
                        StringComparison.OrdinalIgnoreCase);
                }
                else if (xmlContent.Contains("encoding=\"utf-16\"", StringComparison.OrdinalIgnoreCase))
                {
                    xmlContent = xmlContent.Replace(
                        "encoding=\"utf-16\"",
                        "encoding=\"UTF-8\"",
                        StringComparison.OrdinalIgnoreCase);
                }

                return xmlContent.Trim();
            }

            return xmlContent;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts FDA resource URLs to local server URLs for browser viewing.
        /// </summary>
        /// <param name="xmlContent">The XML content with FDA URLs.</param>
        /// <returns>XML content with local resource URLs.</returns>
        /// <remarks>
        /// Replaces FDA stylesheet URL with local path to avoid CORS issues.
        /// Schema location remains unchanged as browsers don't fetch it.
        /// Only modifies the xml-stylesheet processing instruction.
        /// </remarks>
        /// <seealso cref="GenerateXmlDocument"/>
        /// <seealso cref="isBrowserViewRequest"/>
        private string convertToLocalResources(string xmlContent)
        {
            #region implementation

            // Construct base URL from current request
            var request = HttpContext.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";

            // Replace FDA stylesheet URL with local path
#if DEBUG
            xmlContent = xmlContent.Replace(
                "href=\"https://www.accessdata.fda.gov/spl/stylesheet/spl.xsl\"",
                "href=\"/stylesheets/spl.xsl\"");

            // Note: Schema location is NOT replaced - browsers don't fetch XSD files
            // The xsi:schemaLocation remains as FDA URL for validation purposes
#else
            xmlContent = xmlContent.Replace(
               "href=\"https://www.accessdata.fda.gov/spl/stylesheet/spl.xsl\"",
               $"href=\"{baseUrl}/api/stylesheets/spl.xsl\"");

            // Note: Schema location is NOT replaced - browsers don't fetch XSD files
            // The xsi:schemaLocation remains as FDA URL for validation purposes
#endif
            return xmlContent;

#endregion
        }

        #region Document Navigation

        /**************************************************************/
        /// <summary>
        /// Gets document navigation data with version tracking capabilities.
        /// Supports discovery of latest document versions and version history navigation.
        /// </summary>
        /// <param name="latestOnly">
        /// If true, returns only the latest version of each document set.
        /// If false, returns all versions.
        /// </param>
        /// <param name="setGuid">
        /// Optional filter by SetGUID to get all versions of a specific document set.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of document navigation data with version information.</returns>
        /// <response code="200">Returns the document navigation data.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/document/navigation?latestOnly=true
        /// GET /api/Label/document/navigation?latestOnly=false&amp;setGuid=12345678-1234-1234-1234-123456789012
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedDocumentID": "encrypted_string",
        ///     "DocumentGUID": "12345678-1234-1234-1234-123456789012",
        ///     "SetGUID": "abcdefgh-1234-1234-1234-123456789012",
        ///     "VersionNumber": 3,
        ///     "IsLatestVersion": true,
        ///     "EffectiveDate": "2024-01-15"
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by EffectiveDate in descending order.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get only latest document versions
        /// GET /api/Label/document/navigation?latestOnly=true
        ///
        /// // Get all versions of a specific document set
        /// GET /api/Label/document/navigation?latestOnly=false&amp;setGuid=12345678-1234-1234-1234-123456789012
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.GetDocumentNavigationAsync"/>
        /// <seealso cref="LabelView.DocumentNavigation"/>
        /// <seealso cref="Label.Document"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("document/navigation")]
        [ProducesResponseType(typeof(IEnumerable<DocumentNavigationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<DocumentNavigationDto>>> GetDocumentNavigation(
            [FromQuery] bool latestOnly = false,
            [FromQuery] Guid? setGuid = null,
            [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            _logger.LogInformation("Getting document navigation. LatestOnly: {LatestOnly}, SetGUID: {SetGUID}, Page: {PageNumber}, Size: {PageSize}",
                    latestOnly, setGuid, pageNumber, pageSize);

                var results = await DtoLabelAccess.GetDocumentNavigationAsync(
                    _dbContext,
                    latestOnly,
                    setGuid,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

            return Ok(results);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets document version history for a specific document set.
        /// Tracks all versions over time within a SetGUID or for a specific DocumentGUID.
        /// </summary>
        /// <param name="setGuidOrDocumentGuid">
        /// The SetGUID or DocumentGUID to retrieve version history for.
        /// When a DocumentGUID is provided, returns the history for its associated document set.
        /// </param>
        /// <returns>List of document version history records.</returns>
        /// <response code="200">Returns the document version history.</response>
        /// <response code="400">If setGuidOrDocumentGuid is empty.</response>
        /// <response code="404">If no version history is found for the specified GUID.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/document/version-history/12345678-1234-1234-1234-123456789012
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedDocumentID": "encrypted_string",
        ///     "DocumentGUID": "12345678-1234-1234-1234-123456789012",
        ///     "SetGUID": "abcdefgh-1234-1234-1234-123456789012",
        ///     "VersionNumber": 3,
        ///     "EffectiveDate": "2024-01-15",
        ///     "ChangeDescription": "Annual update"
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by VersionNumber in descending order (newest first).
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetDocumentVersionHistoryAsync"/>
        /// <seealso cref="LabelView.DocumentVersionHistory"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("document/version-history/{setGuidOrDocumentGuid}")]
        [ProducesResponseType(typeof(IEnumerable<DocumentVersionHistoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<DocumentVersionHistoryDto>>> GetDocumentVersionHistory(
            Guid setGuidOrDocumentGuid)
        {
            #region Input Validation

            // Validate GUID is not empty
            if (setGuidOrDocumentGuid == Guid.Empty)
            {
                return BadRequest("SetGUID or DocumentGUID cannot be empty.");
            }

            #endregion

            #region Implementation

            _logger.LogInformation("Getting document version history for GUID: {SetGuidOrDocumentGuid}",
                    setGuidOrDocumentGuid);

                var results = await DtoLabelAccess.GetDocumentVersionHistoryAsync(
                    _dbContext,
                    setGuidOrDocumentGuid,
                    _pkEncryptionSecret,
                    _logger);

                // Check if any history was found
                if (results == null || !results.Any())
                {
                    _logger.LogWarning("No version history found for GUID: {SetGuidOrDocumentGuid}", setGuidOrDocumentGuid);
                    return NotFound($"No version history found for GUID {setGuidOrDocumentGuid}.");
                }

            return Ok(results);

            #endregion
        }

        #endregion Document Navigation

        /**************************************************************/
        /// <summary>
        /// Retrieves a single "complete" label structure by its unique document identifier.
        /// Returns a hierarchical object starting with the Document and including all
        /// related child entities (authors, sections, structured bodies, relationships, etc.).
        /// </summary>
        /// <param name="documentGuid">The unique identifier (GUID) for the document to retrieve.</param>
        /// <returns>A complete, hierarchical label object for the specified document.</returns>
        /// <response code="200">Returns the complete label structure for the specified document.</response>
        /// <response code="400">If the document GUID parameter is invalid.</response>
        /// <response code="404">If no document is found with the specified GUID.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/label/single?documentGuid=12345678-1234-1234-1234-123456789012
        ///
        /// This endpoint fetches a deep object graph for a single document. The response includes
        /// the complete hierarchy of structured bodies, authors, relationships, and authenticators.
        /// All primary keys within the structure are encrypted for security.
        /// Use this endpoint when you need to retrieve a specific document by its GUID rather than browsing paginated results.
        /// </remarks>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="Label.Document.DocumentGUID"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("single/{documentGuid}")]
        [ProducesResponseType(typeof(Dictionary<string, object?>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Dictionary<string, object?>>> GetSingleCompleteLabel(Guid documentGuid)
        {
            #region Input Validation
            if (documentGuid == Guid.Empty)
            {
                return BadRequest("Document GUID cannot be empty.");
            }
            #endregion

            #region Implmentation
            // We need the specific repository for Label.Document
            var documentRepository = _serviceProvider.GetRequiredService<Repository<Label.Document>>();

                var completeLabels = await documentRepository.GetCompleteLabelsAsync(documentGuid);

                // Check if document was found
                if (completeLabels == null || !completeLabels.Any())
                {
                    _logger.LogWarning("Document with GUID {DocumentGuid} was not found.", documentGuid);
                    return NotFound($"Document with GUID {documentGuid} was not found.");
                }

                // Return the first (and should be only) document from the result
                var singleDocument = completeLabels.First();

                // Add response headers for tracking
                Response.Headers.Append("X-Document-Guid", documentGuid.ToString());
                Response.Headers.Append("X-Document-Found", "true");

            return Ok(singleDocument);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves a collection of "complete" label structures, with optional paging.
        /// Each item in the collection is a hierarchical object starting with a Document
        /// and including its related child entities (authors, sections, text, etc.).
        /// </summary>
        /// <param name="pageNumber">The 1-based page number to retrieve. Defaults to 1.</param>
        /// <param name="pageSize">The number of records per page. Defaults to 10.</param>
        /// <returns>A list of complete, hierarchical label objects.</returns>
        /// <response code="200">Returns the list of complete labels.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/complete?pageNumber=1&amp;pageSize=5
        ///
        /// This endpoint fetches a deep object graph for each document. The response can be large.
        /// All primary keys within the structure are encrypted for security.
        /// </remarks>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("complete/{pageNumber?}/{pageSize?}")]
        [ProducesResponseType(typeof(IEnumerable<Dictionary<string, object?>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<Dictionary<string, object?>>>> GetCompleteLabels(
            int pageNumber = 1,
            int pageSize = 10)
        {
            #region Input Validation
            if (pageNumber <= 0)
            {
                return BadRequest("Page number must be greater than 0.");
            }
            if (pageSize <= 0)
            {
                return BadRequest("Page size must be greater than 0.");
            }
            #endregion

            #region Implementation
            // We need the specific repository for Label.Document
            var documentRepository = _serviceProvider.GetRequiredService<Repository<Label.Document>>();

                var completeLabels = await documentRepository.GetCompleteLabelsAsync(pageNumber, pageSize);

                int totalCount = completeLabels?.Count() ?? 0;
                Response.Headers.Append("X-Page-Number", (pageNumber).ToString());
                Response.Headers.Append("X-Page-Size", (pageSize).ToString());
                Response.Headers.Append("X-Total-Count", totalCount.ToString());


            return Ok(completeLabels);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates a populated XML document for the specified Label document GUID.
        /// Intelligently serves browser-friendly or FDA-compliant XML based on request context.
        /// </summary>
        /// <param name="documentGuid">The unique identifier for the document to process.</param>
        /// <param name="minify">(OPTIONAL default:false) Compacts XML output in post processing (might be slower)</param>
        /// <returns>HTTP response containing the populated XML document.</returns>
        /// <example>
        /// GET /api/xmldocument/generate/12345678-1234-1234-1234-123456789012
        /// GET /api/xmldocument/generate/12345678-1234-1234-1234-123456789012/true
        /// </example>
        /// <remarks>
        /// **Limitation:** This endpoint only supports SPL Label document types. Other SPL sub-types
        /// (e.g., indexing files, establishment registrations, product listings) are not supported
        /// and will not produce accurate XML output.
        ///
        /// Returns XML with URLs for downloads/validation, or local URLs for browser viewing.
        /// Automatically detects request type based on Accept headers.
        /// Logs processing time and any errors encountered during generation.
        /// </remarks>
        /// <seealso cref="SplExportService"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("generate/{documentGuid:guid}/{minify:bool}")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/xml")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GenerateXmlDocument(Guid documentGuid, bool minify = false)
        {
            #region implementation
            try
            {
                var exportEnabled = _configuration.GetValue<bool>("FeatureFlags:SplExportEnabled", true);

                if (!exportEnabled)
                {
                    return StatusCode(503, new
                    {
                        error = "Export functionality is currently disabled"
                    });
                }

                _logger.LogInformation("Generating XML document for GUID: {DocumentGuid}", documentGuid);
                var startTime = DateTime.UtcNow;

                // Generate XML with FDA-compliant URLs
                var xmlContent = await _splExportService.ExportDocumentToSplAsync(documentGuid, minify);

                // Fix encoding to UTF-8 as required by FDA specification
                xmlContent = ensureUtf8Encoding(xmlContent);

                // Detect if request is from browser for direct viewing
                var isBrowserView = isBrowserViewRequest(Request);

                if (isBrowserView)
                {
                    // Modify URLs to local resources for browser rendering
                    xmlContent = convertToLocalResources(xmlContent);
                    _logger.LogInformation("Converted to browser-friendly format for GUID: {DocumentGuid}", documentGuid);
                }

                var processingTime = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Successfully generated XML document for GUID: {DocumentGuid} in {ProcessingTime}ms (Browser: {IsBrowserView})",
                    documentGuid, processingTime.TotalMilliseconds, isBrowserView);

                // Set proper content type with UTF-8 charset
                return Content(xmlContent, "application/xml; charset=utf-8");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No document found"))
            {
                _logger.LogWarning("Document not found for GUID: {DocumentGuid}", documentGuid);
                return NotFound($"Document not found for GUID: {documentGuid}");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves the original imported XML document for the specified Label document GUID.
        /// Provides a faster alternative to <see cref="GenerateXmlDocument"/> by returning the original
        /// XML stored during import rather than regenerating from database entities.
        /// </summary>
        /// <param name="documentGuid">The unique identifier for the document to retrieve.</param>
        /// <param name="minify">
        /// (OPTIONAL default:false) When true, compacts the XML output by removing unnecessary whitespace,
        /// reducing file size for transmission and storage. Recommended for API/programmatic access.
        /// </param>
        /// <returns>
        /// HTTP response containing the original imported XML document with proper encoding
        /// and stylesheet references for the request context (browser vs. download).
        /// </returns>
        /// <example>
        /// GET /api/Label/original/12345678-1234-1234-1234-123456789012/true
        /// GET /api/Label/original/12345678-1234-1234-1234-123456789012/false
        /// </example>
        /// <remarks>
        /// **Performance Advantage:** This endpoint retrieves the original XML from the SplData table
        /// (a single database read) rather than regenerating the XML from parsed entities, which is
        /// significantly faster and reduces database vCore consumption.
        ///
        /// **Use Cases:**
        /// - AI assistants retrieving label content for analysis
        /// - Quick label preview without full regeneration overhead
        /// - Scenarios where the exact imported XML is preferred over regenerated output
        ///
        /// **Behavior:**
        /// - Returns XML with UTF-8 encoding as required by FDA specification
        /// - When `minify=true`, compacts XML by removing unnecessary whitespace
        /// - Automatically detects browser vs. API requests via Accept headers
        /// - Modifies stylesheet URLs to local resources for browser viewing
        /// - Preserves FDA-compliant URLs for API/download requests
        ///
        /// **Limitation:** Only returns XML for documents that have SplData records. Documents
        /// imported before the SplData feature was implemented may not have original XML available.
        /// </remarks>
        /// <response code="200">Returns the original XML document content.</response>
        /// <response code="400">If the document GUID is invalid.</response>
        /// <response code="404">If no original XML is found for the GUID.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <response code="503">If export functionality is currently disabled.</response>
        /// <seealso cref="GenerateXmlDocument"/>
        /// <seealso cref="SplDataService.GetSplDataByGuidAsync"/>
        /// <seealso cref="SplData.SplXML"/>
        [HttpGet("original/{documentGuid:guid}/{minify:bool}")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/xml")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> OriginalXmlDocument(Guid documentGuid, bool minify = false)
        {
            #region implementation
            try
            {
                // Check if export functionality is enabled
                var exportEnabled = _configuration.GetValue<bool>("FeatureFlags:SplExportEnabled", true);

                if (!exportEnabled)
                {
                    return StatusCode(503, new
                    {
                        error = "Export functionality is currently disabled"
                    });
                }

                _logger.LogInformation("Retrieving original XML document for GUID: {DocumentGuid}", documentGuid);
                var startTime = DateTime.UtcNow;

                // Retrieve the original XML from SplData table
                var splData = await _splDataService.GetSplDataByGuidAsync(documentGuid);

                if (splData == null || string.IsNullOrEmpty(splData.SplXML))
                {
                    _logger.LogWarning("Original XML not found for GUID: {DocumentGuid}", documentGuid);
                    return NotFound($"Original XML document not found for GUID: {documentGuid}");
                }

                var xmlContent = splData.SplXML;

                // Fix encoding to UTF-8 as required by FDA specification
                xmlContent = ensureUtf8Encoding(xmlContent);

                // Optional: Minify the XML output if requested to reduce size for transmission/storage
                if (minify)
                {
                    xmlContent = xmlContent.MinifyXml() ?? string.Empty;
                }

                // Detect if request is from browser for direct viewing
                var isBrowserView = isBrowserViewRequest(Request);

                if (isBrowserView)
                {
                    // Modify URLs to local resources for browser rendering
                    xmlContent = convertToLocalResources(xmlContent);
                    _logger.LogInformation("Converted to browser-friendly format for GUID: {DocumentGuid}", documentGuid);
                }

                var processingTime = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Successfully retrieved original XML document for GUID: {DocumentGuid} in {ProcessingTime}ms (Browser: {IsBrowserView}, Minified: {Minify})",
                    documentGuid, processingTime.TotalMilliseconds, isBrowserView, minify);

                // Set proper content type with UTF-8 charset
                return Content(xmlContent, "application/xml; charset=utf-8");
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument for original XML retrieval: {DocumentGuid}", documentGuid);
                return BadRequest($"Invalid document GUID: {documentGuid}");
            }
            #endregion
        }

        #endregion
    }
}
