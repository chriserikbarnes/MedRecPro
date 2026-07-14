using MedRecPro.Controllers;
using MedRecPro.DataAccess;
using MedRecPro.Filters;
using MedRecPro.Models;
using MedRecPro.Service;
using MedRecPro.Service.LabelQuery;
using Microsoft.AspNetCore.Mvc;

namespace MedRecPro.Api.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Handles markdown-oriented Label endpoints while preserving the original Label route surface.
    /// </summary>
    /// <remarks>
    /// The <see cref="LabelFeatureControllerAttribute"/> marker pins this controller to the public <c>Label</c> controller
    /// name so inherited routes remain <c>api/Label</c> in Debug builds and <c>Label</c> in Release builds.
    /// </remarks>
    /// <seealso cref="LabelFeatureControllerAttribute"/>
    /// <seealso cref="LabelController"/>
    /// <seealso cref="DtoLabelAccess"/>
    [ApiController]
    [LabelFeatureController]
    [LabelFeatureSwaggerTag("Label Markdown")]
    public class LabelMarkdownController : ApiControllerBase
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Logger instance for markdown endpoint diagnostics.
        /// </summary>
        /// <seealso cref="ILogger"/>
        private readonly ILogger<LabelMarkdownController> _logger;

        /**************************************************************/
        /// <summary>
        /// Service for Claude AI markdown cleanup.
        /// </summary>
        /// <seealso cref="IClaudeApiService"/>
        private readonly IClaudeApiService _claudeApiService;

        /**************************************************************/
        /// <summary>Provides markdown retrieval and generation operations.</summary>
        private readonly ILabelMarkdownService _labelMarkdownService;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="LabelMarkdownController"/> class.
        /// </summary>
        /// <param name="logger">Logger instance for markdown endpoint diagnostics.</param>
        /// <param name="claudeApiService">Claude API service used for clean display markdown generation.</param>
        /// <param name="labelMarkdownService">Markdown query service for Label endpoints.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
        /// <seealso cref="LabelController"/>
        public LabelMarkdownController(
            ILogger<LabelMarkdownController> logger,
            IClaudeApiService claudeApiService,
            ILabelMarkdownService labelMarkdownService)
        {
            #region implementation

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _claudeApiService = claudeApiService ?? throw new ArgumentNullException(nameof(claudeApiService));
            _labelMarkdownService = labelMarkdownService ?? throw new ArgumentNullException(nameof(labelMarkdownService));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets markdown-formatted section content for a document by DocumentGUID.
        /// Returns aggregated, LLM-ready section text from the vw_LabelSectionMarkdown view.
        /// </summary>
        /// <param name="documentGuid">
        /// The unique identifier (GUID) for the document to retrieve sections for.
        /// </param>
        /// <param name="sectionCode">
        /// Optional LOINC section code to filter results (e.g., "34067-9" for Indications).
        /// When provided, only sections matching this code are returned, significantly reducing payload size.
        /// </param>
        /// <returns>List of section markdown DTOs with formatted content.</returns>
        /// <response code="200">Returns the list of markdown-formatted sections.</response>
        /// <response code="400">If documentGuid is not a valid GUID.</response>
        /// <response code="404">If no sections are found for the document.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ### Get Label Sections as Markdown
        ///
        /// This endpoint returns all sections of a drug label formatted as markdown,
        /// designed for AI/LLM consumption. Each section includes:
        /// - **SectionKey**: Unique identifier combining DocumentGUID, SectionCode, and SectionTitle
        /// - **FullSectionText**: Complete markdown text with ## header and content
        /// - **ContentBlockCount**: Number of content blocks aggregated
        ///
        /// ### Example
        ///
        /// ```
        /// GET /api/Label/markdown/sections/052493C7-89A3-452E-8140-04DD95F0D9E2
        ///
        /// // Filter to specific section (reduces payload significantly)
        /// GET /api/Label/markdown/sections/052493C7-89A3-452E-8140-04DD95F0D9E2?sectionCode=34067-9
        /// ```
        ///
        /// ### Response
        ///
        /// ```json
        /// [
        ///   {
        ///     "LabelSectionMarkdown": {
        ///       "DocumentGUID": "052493C7-89A3-452E-8140-04DD95F0D9E2",
        ///       "SectionCode": "34067-9",
        ///       "SectionTitle": "INDICATIONS AND USAGE",
        ///       "SectionKey": "052493C7-89A3-452E-8140-04DD95F0D9E2|34067-9|INDICATIONS AND USAGE",
        ///       "FullSectionText": "## INDICATIONS AND USAGE\n\nLIPITOR is indicated...",
        ///       "ContentBlockCount": 5
        ///     }
        ///   }
        /// ]
        /// ```
        ///
        /// ### Use Case
        ///
        /// This endpoint is designed for AI skill augmentation workflows where the Claude API
        /// needs authoritative label content rather than relying on training data to generate
        /// accurate summaries and descriptions.
        ///
        /// ### Token Optimization
        ///
        /// When comparing multiple drugs, use the `sectionCode` parameter to fetch only
        /// the relevant section(s). This reduces payload from ~88KB (all sections) to ~1-2KB
        /// per section, significantly reducing token usage for AI skill augmentation.
        ///
        /// **Common LOINC Section Codes:**
        /// - `34067-9` = Indications and Usage
        /// - `34084-4` = Adverse Reactions
        /// - `34070-3` = Contraindications
        /// - `43685-7` = Warnings and Precautions
        /// - `34068-7` = Dosage and Administration
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetLabelSectionMarkdownAsync"/>
        /// <seealso cref="LabelView.LabelSectionMarkdown"/>
        /// <seealso cref="GetLabelMarkdownExport"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("markdown/sections/{documentGuid:guid}")]
        [ProducesResponseType(typeof(IEnumerable<LabelSectionMarkdownDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<LabelSectionMarkdownDto>>> GetLabelSectionMarkdown(
            [FromRoute] Guid documentGuid,
            [FromQuery] string? sectionCode = null)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(sectionCode))
            {
                _logger.LogInformation("Getting all markdown sections for DocumentGUID: {DocumentGuid}", documentGuid);
            }
            else
            {
                _logger.LogInformation("Getting markdown section {SectionCode} for DocumentGUID: {DocumentGuid}", sectionCode, documentGuid);
            }

            var results = await _labelMarkdownService.GetLabelSectionMarkdownAsync(documentGuid, sectionCode);

            // Return 404 if no sections found.
            if (results == null || results.Count == 0)
            {
                var message = string.IsNullOrWhiteSpace(sectionCode)
                    ? $"No sections found for DocumentGUID {documentGuid}."
                    : $"No sections found for DocumentGUID {documentGuid} with SectionCode {sectionCode}.";
                return NotFound(message);
            }

            return Ok(results);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates a complete markdown document for a drug label by DocumentGUID.
        /// Combines all sections with header information for AI skill augmentation.
        /// </summary>
        /// <param name="documentGuid">
        /// The unique identifier (GUID) for the document to export.
        /// </param>
        /// <returns>A LabelMarkdownExportDto containing the complete markdown and metadata.</returns>
        /// <response code="200">Returns the complete markdown export.</response>
        /// <response code="400">If documentGuid is not a valid GUID.</response>
        /// <response code="404">If no document is found for the GUID.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ### Generate Complete Label Markdown Export
        ///
        /// This endpoint generates a complete markdown document suitable for AI/LLM consumption.
        /// The output includes:
        ///
        /// **Header Section:**
        /// - Document title and identifiers
        /// - Data dictionary explaining the structure
        /// - Section count and content block totals
        ///
        /// **Content Sections:**
        /// - All label sections in LOINC code order
        /// - Each section with ## markdown header
        /// - Content with markdown formatting (bold, italics, underline)
        ///
        /// ### Example
        ///
        /// ```
        /// GET /api/Label/markdown/export/052493C7-89A3-452E-8140-04DD95F0D9E2
        /// ```
        ///
        /// ### Response
        ///
        /// ```json
        /// {
        ///   "documentGUID": "052493C7-89A3-452E-8140-04DD95F0D9E2",
        ///   "setGUID": "A1B2C3D4-E5F6-7890-ABCD-EF1234567890",
        ///   "documentTitle": "LIPITOR- atorvastatin calcium tablet",
        ///   "sectionCount": 15,
        ///   "totalContentBlocks": 87,
        ///   "fullMarkdown": "# FDA Drug Label Reference\n\n..."
        /// }
        /// ```
        ///
        /// ### Use Case
        ///
        /// This endpoint is designed for building Claude API skills that need to summarize
        /// or analyze FDA drug label content accurately and completely, without relying
        /// on AI training data which may be outdated or incomplete.
        ///
        /// **Workflow:**
        /// 1. Query this endpoint with the DocumentGUID of the label
        /// 2. Pass the `fullMarkdown` content to your Claude API skill
        /// 3. Use the authoritative content for accurate summarization
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GenerateLabelMarkdownAsync"/>
        /// <seealso cref="LabelMarkdownExportDto"/>
        /// <seealso cref="GetLabelSectionMarkdown"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("markdown/export/{documentGuid:guid}")]
        [ProducesResponseType(typeof(LabelMarkdownExportDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<LabelMarkdownExportDto>> GetLabelMarkdownExport(
            [FromRoute] Guid documentGuid)
        {
            #region implementation

            _logger.LogInformation("Generating markdown export for DocumentGUID: {DocumentGuid}", documentGuid);

            var result = await _labelMarkdownService.GenerateLabelMarkdownAsync(documentGuid);

            // Return 404 if no content generated (empty document).
            if (result == null || result.SectionCount == 0)
            {
                return NotFound($"No sections found for DocumentGUID {documentGuid}.");
            }

            return Ok(result);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Downloads the complete markdown export as a .md file.
        /// Returns the markdown content with appropriate content-type for download.
        /// </summary>
        /// <param name="documentGuid">
        /// The unique identifier (GUID) for the document to export.
        /// </param>
        /// <returns>A downloadable markdown file.</returns>
        /// <response code="200">Returns the markdown file for download.</response>
        /// <response code="400">If documentGuid is not a valid GUID.</response>
        /// <response code="404">If no document is found for the GUID.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ### Download Label as Markdown File
        ///
        /// This endpoint returns the same content as `/api/Label/markdown/export/{documentGuid}`
        /// but formatted as a downloadable .md file with appropriate headers.
        ///
        /// ### Example
        ///
        /// ```
        /// GET /api/Label/markdown/download/052493C7-89A3-452E-8140-04DD95F0D9E2
        /// ```
        ///
        /// ### Response Headers
        ///
        /// ```
        /// Content-Type: text/markdown
        /// Content-Disposition: attachment; filename="LIPITOR-label.md"
        /// ```
        ///
        /// ### Use Case
        ///
        /// This endpoint allows users to download the complete label markdown for:
        /// - Offline AI skill development and testing
        /// - Local storage of authoritative label content
        /// - Integration with documentation systems
        /// </remarks>
        /// <seealso cref="GetLabelMarkdownExport"/>
        /// <seealso cref="DtoLabelAccess.GenerateLabelMarkdownAsync"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("markdown/download/{documentGuid:guid}")]
        [Produces("text/markdown")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DownloadLabelMarkdown(
            [FromRoute] Guid documentGuid)
        {
            #region implementation

            _logger.LogInformation("Downloading markdown for DocumentGUID: {DocumentGuid}", documentGuid);

            var result = await _labelMarkdownService.GenerateLabelMarkdownAsync(documentGuid);

            // Return 404 if no content generated (empty document).
            if (result == null || result.SectionCount == 0)
            {
                return NotFound($"No sections found for DocumentGUID {documentGuid}.");
            }

            // Generate safe filename from document title.
            var safeTitle = result.DocumentTitle?.Replace(" ", "-")
                .Replace(",", "")
                .Replace("/", "-")
                .Replace("\\", "-")
                ?? "drug-label";

            // Truncate if too long.
            if (safeTitle.Length > 50)
            {
                safeTitle = safeTitle.Substring(0, 50);
            }

            var fileName = $"{safeTitle}-label.md";

            // Return as file download.
            var bytes = System.Text.Encoding.UTF8.GetBytes(result.FullMarkdown ?? "");
            return File(bytes, "text/markdown", fileName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates clean, human-readable markdown for display by processing content through Claude AI.
        /// Returns formatted markdown suitable for static web app rendering.
        /// </summary>
        /// <param name="documentGuid">
        /// The unique identifier (GUID) for the document to export.
        /// </param>
        /// <returns>Clean, formatted markdown text suitable for display.</returns>
        /// <response code="200">Returns the clean markdown content.</response>
        /// <response code="400">If documentGuid is not a valid GUID.</response>
        /// <response code="404">If no document is found for the GUID.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ### Generate Clean Display Markdown
        ///
        /// This endpoint generates clean, human-readable markdown by:
        /// 1. Retrieving all sections from vw_LabelSectionMarkdown
        /// 2. Passing the raw content to Claude AI for cleanup
        /// 3. Returning formatted markdown without XML/HTML artifacts
        ///
        /// The output is optimized for:
        /// - **Static web app display** (React/Angular markdown renderers)
        /// - **Documentation generation**
        /// - **Human readability** without technical artifacts
        ///
        /// ### Example
        ///
        /// ```
        /// GET /api/Label/markdown/display/22212bf6-1414-4b32-bc67-25d614c357ee
        /// ```
        ///
        /// ### Response (text/markdown)
        ///
        /// ```markdown
        /// # Lorazepam Tablets, USP CIV
        ///
        /// **Rx only**
        ///
        /// ## INDICATIONS AND USAGE
        ///
        /// Lorazepam tablets are indicated for the management of anxiety disorders...
        ///
        /// ## DOSAGE AND ADMINISTRATION
        ///
        /// Lorazepam tablets are administered orally...
        /// ```
        ///
        /// ### Performance Notes
        ///
        /// - Results are cached for 1 hour to minimize Claude API calls
        /// - First request may take 5-15 seconds for Claude processing
        /// - Subsequent requests return cached content instantly
        ///
        /// ### Use Case
        ///
        /// This endpoint is designed for displaying drug labels in static web applications
        /// where clean, readable markdown is preferred over XML-to-XSLT transformation for simple display scenarios.
        /// </remarks>
        /// <seealso cref="GetLabelMarkdownExport"/>
        /// <seealso cref="DownloadLabelMarkdown"/>
        /// <seealso cref="DtoLabelAccess.GenerateCleanLabelMarkdownAsync"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("markdown/display/{documentGuid:guid}")]
        [Produces("text/markdown")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCleanLabelMarkdown(
            [FromRoute] Guid documentGuid)
        {
            #region implementation

            _logger.LogInformation("Generating clean display markdown for DocumentGUID: {DocumentGuid}", documentGuid);

            var cleanMarkdown = await _labelMarkdownService.GenerateCleanLabelMarkdownAsync(documentGuid, _claudeApiService);

            // Return 404 if no content generated (empty document).
            if (string.IsNullOrWhiteSpace(cleanMarkdown))
            {
                return NotFound($"No sections found for DocumentGUID {documentGuid}.");
            }

            // Return as text/markdown content.
            return Content(cleanMarkdown, "text/markdown", System.Text.Encoding.UTF8);

            #endregion
        }

        #endregion
    }
}
