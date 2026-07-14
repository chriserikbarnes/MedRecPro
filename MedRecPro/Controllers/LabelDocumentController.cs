using MedRecPro.Controllers;
using MedRecPro.Filters;
using MedRecPro.Models;
using MedRecPro.Service;
using MedRecPro.Service.LabelQuery;
using Microsoft.AspNetCore.Mvc;

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
        /// Service for complete Label document graph retrieval.
        /// </summary>
        /// <seealso cref="ICompleteLabelService"/>
        private readonly ICompleteLabelService _completeLabelService;

        /**************************************************************/
        /// <summary>Provides complete document graph and navigation queries.</summary>
        private readonly ILabelDocumentQueryService _labelDocumentQueryService;

        /**************************************************************/
        /// <summary>Provides generated and original XML retrieval outcomes.</summary>
        private readonly ILabelXmlDocumentService _labelXmlDocumentService;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="LabelDocumentController"/> class.
        /// </summary>
        /// <param name="completeLabelService">Service for complete document graph retrieval.</param>
        /// <param name="labelDocumentQueryService">Document query service for graph and navigation reads.</param>
        /// <param name="labelXmlDocumentService">XML retrieval service for generated and original documents.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
        /// <seealso cref="LabelController"/>
        public LabelDocumentController(
            ICompleteLabelService completeLabelService,
            ILabelDocumentQueryService labelDocumentQueryService,
            ILabelXmlDocumentService labelXmlDocumentService)
        {
            #region implementation

            _completeLabelService = completeLabelService ?? throw new ArgumentNullException(nameof(completeLabelService));
            _labelDocumentQueryService = labelDocumentQueryService ?? throw new ArgumentNullException(nameof(labelDocumentQueryService));
            _labelXmlDocumentService = labelXmlDocumentService ?? throw new ArgumentNullException(nameof(labelXmlDocumentService));

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
        /// <seealso cref="ILabelDocumentQueryService.GetDocumentNavigationAsync"/>
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

                var results = await _labelDocumentQueryService.GetDocumentNavigationAsync(latestOnly, setGuid, pageNumber, pageSize);

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
        /// <seealso cref="ILabelDocumentQueryService.GetDocumentVersionHistoryAsync"/>
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

                var results = await _labelDocumentQueryService.GetDocumentVersionHistoryAsync(setGuidOrDocumentGuid);

                // Check if any history was found
                if (results == null || !results.Any())
                {
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
                var completeLabels = await _completeLabelService.GetAsync(documentGuid);

                // Check if document was found
                if (completeLabels == null || !completeLabels.Any())
                {
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
                var completeLabels = await _completeLabelService.GetAsync(pageNumber, pageSize);

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
            var outcome = await _labelXmlDocumentService.GetGeneratedAsync(documentGuid, minify, HttpContext.RequestAborted);
            if (outcome.Status == LabelXmlDocumentStatus.Disabled)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = outcome.Error });
            }

            if (outcome.Status == LabelXmlDocumentStatus.NotFound)
            {
                return NotFound(outcome.Error);
            }

            var xmlContent = outcome.Xml!;
            if (isBrowserViewRequest(Request))
            {
                xmlContent = convertToLocalResources(xmlContent);
            }

            return Content(xmlContent, "application/xml; charset=utf-8");
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
            var outcome = await _labelXmlDocumentService.GetOriginalAsync(documentGuid, minify, HttpContext.RequestAborted);
            if (outcome.Status == LabelXmlDocumentStatus.Disabled)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = outcome.Error });
            }

            if (outcome.Status == LabelXmlDocumentStatus.InvalidInput)
            {
                return BadRequest(outcome.Error);
            }

            if (outcome.Status == LabelXmlDocumentStatus.NotFound)
            {
                return NotFound(outcome.Error);
            }

            var xmlContent = outcome.Xml!;
            if (isBrowserViewRequest(Request))
            {
                xmlContent = convertToLocalResources(xmlContent);
            }

            return Content(xmlContent, "application/xml; charset=utf-8");
            #endregion
        }

        #endregion
    }
}
