using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using RazorLight;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Service for exporting label documents from the database into properly formatted 
    /// SPL (Structured Product Labeling) XML files following FDA specifications.
    /// Transforms database DTOs into SPL-compliant XML structures for regulatory submission.
    /// </summary>
    /// <remarks>
    /// This service orchestrates the complete transformation pipeline from database
    /// entities to SPL XML, ensuring compliance with HL7 v3 standards and FDA requirements.
    /// Handles encrypted ID decryption to establish proper entity relationships.
    /// </remarks>
    /// <example>
    /// <code>
    /// var splService = new SplExportService(db, pkSecret, logger);
    /// var xmlContent = await splService.ExportDocumentToSplAsync(documentGuid);
    /// await File.WriteAllTextAsync($"{documentGuid}.xml", xmlContent);
    /// </code>
    /// </example>
    /// <seealso cref="Label.Document"/>
    /// <seealso cref="SplDocumentDto"/>
    /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync(ApplicationDbContext, Guid, string, ILogger)"/>
    public class SplExportService
    {
        #region implementation

        private readonly ApplicationDbContext _db;
        private readonly string _pkSecret;
        private readonly ILogger _logger;
        private readonly StringCipher _cipher;
        private readonly IRazorLightEngine _razorEngine;

        // Razor template paths
        private const string SPL_VIEW = "Views";
        private const string SPL_TEMPLATE_NAME = "SplTemplates";

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SPL export service with required dependencies.
        /// </summary>
        /// <param name="db">The application database context for data access.</param>
        /// <param name="pkSecret">Secret key used for ID encryption/decryption.</param>
        /// <param name="logger">Logger instance for diagnostic and error logging.</param>
        /// <seealso cref="ApplicationDbContext"/>
        public SplExportService(ApplicationDbContext db, string pkSecret, ILogger logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _pkSecret = pkSecret ?? throw new ArgumentNullException(nameof(pkSecret));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cipher = new StringCipher();

            // Point the engine to the physical location of your templates
            var projectRoot = Directory.GetCurrentDirectory();
            var templateFolderPath = Path.Combine(projectRoot, SPL_VIEW, SPL_TEMPLATE_NAME);

            _razorEngine = new RazorLightEngineBuilder()
                .UseFileSystemProject(templateFolderPath)
                .UseMemoryCachingProvider()
                .EnableDebugMode() // Enable debug mode for better error messages
                .Build();
        }

        /**************************************************************/
        /// <summary>
        /// Exports a document from the database as SPL XML format by its GUID identifier.
        /// Main entry point for SPL export functionality that orchestrates the complete
        /// transformation from database entities to formatted XML output.
        /// </summary>
        /// <param name="documentGuid">The unique identifier for the document to export.</param>
        /// <returns>SPL-formatted XML string ready for FDA submission or file storage.</returns>
        /// <example>
        /// <code>
        /// var documentGuid = Guid.Parse("A4FD2D68-019F-4C89-8923-4E61262F6EEE");
        /// string splXml = await ExportDocumentToSplAsync(documentGuid);
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs the complete export pipeline:
        /// 1. Fetches document data from database using BuildDocumentsAsync
        /// 2. Serializes to XML with proper namespaces and formatting with Razor templates
        /// </remarks>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync(ApplicationDbContext, Guid, string, ILogger)"/>
        public async Task<string> ExportDocumentToSplAsync(Guid documentGuid)
        {
            #region implementation

            _logger.LogInformation("Starting SPL export for document {DocumentGuid}", documentGuid);

            try
            {
                // Fetch document data from database
                List<DocumentDto>? documents = await DtoLabelAccess.BuildDocumentsAsync(_db, documentGuid, _pkSecret, _logger);

                if (documents == null || !documents.Any())
                {
                    throw new InvalidOperationException($"Document with GUID {documentGuid} not found");
                }

                DocumentDto? documentDto = documents.FirstOrDefault();

                if (documentDto == null)
                {
                    throw new InvalidOperationException($"No document data found for GUID {documentGuid}");
                }

                // Serialize to XML using the corrected template name without path prefix
                string xmlContent = await _razorEngine.CompileRenderAsync("GenerateSpl", documentDto);

                _logger.LogInformation("Successfully exported document {DocumentGuid} to SPL XML", documentGuid);

                return xmlContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting document {DocumentGuid} to SPL", documentGuid);
                throw;
            }

            #endregion
        }

        #endregion
    }
}