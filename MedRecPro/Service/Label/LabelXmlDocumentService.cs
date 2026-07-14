using MedRecPro.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Identifies the established result of an XML document retrieval operation.
    /// </summary>
    /// <seealso cref="LabelXmlDocumentOutcome"/>
    public enum LabelXmlDocumentStatus
    {
        Success,
        Disabled,
        InvalidInput,
        NotFound
    }

    /**************************************************************/
    /// <summary>
    /// Carries an XML document retrieval outcome to the HTTP adapter.
    /// </summary>
    /// <param name="Status">The classified retrieval result.</param>
    /// <param name="Xml">The normalized XML for a successful result.</param>
    /// <param name="Error">The established failure message.</param>
    /// <seealso cref="LabelXmlDocumentStatus"/>
    public sealed record LabelXmlDocumentOutcome(LabelXmlDocumentStatus Status, string? Xml = null, string? Error = null);

    /**************************************************************/
    /// <summary>
    /// Retrieves generated and originally imported Label XML below the MVC boundary.
    /// </summary>
    /// <remarks>
    /// Feature-gate evaluation, document lookup, minification, UTF-8 declaration normalization, and
    /// expected retrieval failure classification live here. Controllers retain browser-specific URL
    /// rewriting and result creation because those concerns depend on the current HTTP request.
    /// </remarks>
    /// <seealso cref="ISplExportService"/>
    /// <seealso cref="SplDataService"/>
    public interface ILabelXmlDocumentService
    {
        /**************************************************************/
        /// <summary>Generates an SPL XML document for a Label document.</summary>
        /// <param name="documentGuid">The document identifier to render.</param>
        /// <param name="minify">Whether the rendered XML should be compacted.</param>
        /// <param name="cancellationToken">Cancellation propagated from the request boundary.</param>
        /// <returns>A classified generated-XML outcome.</returns>
        /// <seealso cref="ISplExportService.ExportDocumentToSplAsync(Guid, bool)"/>
        Task<LabelXmlDocumentOutcome> GetGeneratedAsync(Guid documentGuid, bool minify, CancellationToken cancellationToken);

        /**************************************************************/
        /// <summary>Retrieves the original imported SPL XML for a Label document.</summary>
        /// <param name="documentGuid">The document identifier to retrieve.</param>
        /// <param name="minify">Whether the original XML should be compacted.</param>
        /// <param name="cancellationToken">Cancellation propagated from the request boundary.</param>
        /// <returns>A classified original-XML outcome.</returns>
        /// <seealso cref="SplDataService.GetSplDataByGuidAsync(Guid)"/>
        Task<LabelXmlDocumentOutcome> GetOriginalAsync(Guid documentGuid, bool minify, CancellationToken cancellationToken);
    }

    /**************************************************************/
    /// <summary>
    /// Implements XML retrieval using the established SPL rendering and data services.
    /// </summary>
    /// <seealso cref="ILabelXmlDocumentService"/>
    public sealed class LabelXmlDocumentService : ILabelXmlDocumentService
    {
        private readonly IConfiguration _configuration;
        private readonly ISplExportService _splExportService;
        private readonly SplDataService _splDataService;
        private readonly ILogger<LabelXmlDocumentService> _logger;

        /**************************************************************/
        /// <summary>Initializes the XML document retrieval service.</summary>
        /// <param name="configuration">Configuration containing the SPL export feature gate.</param>
        /// <param name="splExportService">Service that renders generated SPL XML.</param>
        /// <param name="splDataService">Service that retrieves original imported SPL XML.</param>
        /// <param name="logger">Logger for retrieval diagnostics.</param>
        /// <seealso cref="ISplExportService"/>
        public LabelXmlDocumentService(IConfiguration configuration, ISplExportService splExportService, SplDataService splDataService, ILogger<LabelXmlDocumentService> logger)
        {
            #region implementation

            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _splExportService = splExportService ?? throw new ArgumentNullException(nameof(splExportService));
            _splDataService = splDataService ?? throw new ArgumentNullException(nameof(splDataService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<LabelXmlDocumentOutcome> GetGeneratedAsync(Guid documentGuid, bool minify, CancellationToken cancellationToken)
        {
            #region implementation

            if (!isExportEnabled())
            {
                return disabled();
            }

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var xml = await _splExportService.ExportDocumentToSplAsync(documentGuid, minify).ConfigureAwait(false);
                return success(ensureUtf8Encoding(xml));
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains("No document found", StringComparison.Ordinal))
            {
                _logger.LogWarning(exception, "Document not found while generating SPL XML for {DocumentGuid}.", documentGuid);
                return notFound($"Document not found for GUID: {documentGuid}");
            }

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<LabelXmlDocumentOutcome> GetOriginalAsync(Guid documentGuid, bool minify, CancellationToken cancellationToken)
        {
            #region implementation

            if (!isExportEnabled())
            {
                return disabled();
            }

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var splData = await _splDataService.GetSplDataByGuidAsync(documentGuid).ConfigureAwait(false);
                if (splData is null || string.IsNullOrEmpty(splData.SplXML))
                {
                    return notFound($"Original XML document not found for GUID: {documentGuid}");
                }

                var xml = ensureUtf8Encoding(splData.SplXML);
                return success(minify ? xml.MinifyXml() ?? string.Empty : xml);
            }
            catch (ArgumentException exception)
            {
                _logger.LogWarning(exception, "Invalid original XML retrieval request for {DocumentGuid}.", documentGuid);
                return invalid($"Invalid document GUID: {documentGuid}");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>Checks the established SPL export feature gate.</summary>
        /// <returns>True when document XML export is enabled.</returns>
        /// <seealso cref="IConfiguration"/>
        private bool isExportEnabled()
        {
            #region implementation

            return _configuration.GetValue<bool>("FeatureFlags:SplExportEnabled", true);

            #endregion
        }

        /**************************************************************/
        /// <summary>Normalizes an XML declaration to the established UTF-8 encoding.</summary>
        /// <param name="xmlContent">The XML to normalize.</param>
        /// <returns>Trimmed XML with an UTF-8 declaration where applicable.</returns>
        /// <seealso cref="ILabelXmlDocumentService"/>
        private static string ensureUtf8Encoding(string xmlContent)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                return xmlContent;
            }

            return xmlContent
                .Replace("encoding=\"UTF-16\"", "encoding=\"UTF-8\"", StringComparison.OrdinalIgnoreCase)
                .Trim();

            #endregion
        }

        /**************************************************************/
        /// <summary>Creates a successful XML document outcome.</summary>
        /// <param name="xml">The normalized XML content.</param>
        /// <returns>A success outcome.</returns>
        private static LabelXmlDocumentOutcome success(string xml) => new(LabelXmlDocumentStatus.Success, Xml: xml);

        /**************************************************************/
        /// <summary>Creates the established disabled-feature XML outcome.</summary>
        /// <returns>A disabled outcome.</returns>
        private static LabelXmlDocumentOutcome disabled() => new(LabelXmlDocumentStatus.Disabled, Error: "Export functionality is currently disabled");

        /**************************************************************/
        /// <summary>Creates an invalid-input XML outcome.</summary>
        /// <param name="error">The established validation message.</param>
        /// <returns>An invalid-input outcome.</returns>
        private static LabelXmlDocumentOutcome invalid(string error) => new(LabelXmlDocumentStatus.InvalidInput, Error: error);

        /**************************************************************/
        /// <summary>Creates a not-found XML outcome.</summary>
        /// <param name="error">The established not-found message.</param>
        /// <returns>A not-found outcome.</returns>
        private static LabelXmlDocumentOutcome notFound(string error) => new(LabelXmlDocumentStatus.NotFound, Error: error);
    }
}
