using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace MedRecPro.Service
{
    #region comparison service implementation

    /**************************************************************/
    /// <summary>
    /// Provides comprehensive SPL (Structured Product Labeling) comparison services that leverage
    /// artificial intelligence to analyze data completeness and accuracy between XML and JSON formats.
    /// This enhanced service orchestrates both standard XML-to-JSON comparison and specialized
    /// XML-to-DTO transformation analysis for medical record validation systems.
    /// </summary>
    /// <remarks>
    /// The ComparisonService serves as the primary implementation of medical document comparison functionality,
    /// integrating multiple services to provide end-to-end validation of SPL data transformations.
    /// It ensures that critical medical information including drug safety data, clinical trial results,
    /// dosage instructions, and regulatory compliance details are accurately preserved during format conversions.
    /// 
    /// Enhanced capabilities include:
    /// - Standard SPL XML to generated JSON comparison analysis
    /// - Document-specific XML to DTO transformation validation
    /// - AI-powered analysis with medical terminology understanding
    /// - Structured result parsing and metric extraction
    /// - Comprehensive error handling and diagnostic logging
    /// </remarks>
    /// <seealso cref="IComparisonService"/>
    /// <seealso cref="ComparisonRequest"/>
    /// <seealso cref="ComparisonResponse"/>
    /// <seealso cref="DocumentComparisonResult"/>
    /// <seealso cref="IClaudeApiService"/>
    /// <seealso cref="SplDataService"/>
    public class ComparisonService : IComparisonService
    {
        #region dependency injection fields

        /// <summary>
        /// Logger instance for capturing comparison service operations and diagnostics.
        /// </summary>
        /// <seealso cref="ILogger"/>
        private readonly ILogger<ComparisonService> _logger;

        /// <summary>
        /// SPL data service for managing SPL data retrieval and operations.
        /// </summary>
        /// <seealso cref="SplDataService"/>
        private readonly SplDataService _splDataService;

        /// <summary>
        /// Claude API service for AI-powered comparison analysis operations.
        /// </summary>
        /// <seealso cref="IClaudeApiService"/>
        private readonly IClaudeApiService _claudeApiService;

        /// <summary>
        /// Configuration settings for comparison behavior and processing parameters.
        /// </summary>
        /// <seealso cref="ComparisonSettings"/>
        private readonly MedRecPro.Models.ComparisonSettings _settings;

        /// <summary>
        /// Service provider for resolving additional dependencies during document comparison operations.
        /// </summary>
        /// <seealso cref="IServiceProvider"/>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Provider for SPL label generationg services
        /// </summary>
        private readonly ISplExportService _splExportService;

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the ComparisonService class with required dependencies
        /// for performing AI-powered SPL document comparison operations.
        /// </summary>
        /// <param name="logger">
        /// Logger instance for capturing service operations, errors, and diagnostic information
        /// throughout the comparison workflow.
        /// </param>
        /// <param name="splDataService">
        /// Service responsible for managing SPL data operations including retrieving SPL records
        /// by GUID and accessing XML content from the database.
        /// </param>
        /// <param name="claudeApiService">
        /// AI service interface for performing intelligent comparison analysis between
        /// SPL XML and JSON representations using Claude's language model capabilities.
        /// </param>
        /// <param name="settings">
        /// Configuration options containing comparison behavior settings, processing limits,
        /// caching preferences, and other operational parameters.
        /// </param>
        /// <param name="serviceProvider">
        /// Service provider for resolving additional dependencies such as repositories
        /// during document comparison operations.
        /// </param>
        /// <param name="splExportService">Service to generate SPL XML render from
        /// imported XML data
        /// </param>
        /// <remarks>
        /// This constructor follows dependency injection patterns to ensure loose coupling
        /// and testability. All dependencies are required and validated for null values
        /// through the dependency injection container configuration.
        /// </remarks>
        /// <seealso cref="ILogger{TCategoryName}"/>
        /// <seealso cref="SplDataService"/>
        /// <seealso cref="IClaudeApiService"/>
        /// <seealso cref="IOptions{TOptions}"/>
        /// <seealso cref="IServiceProvider"/>
        public ComparisonService(
            ILogger<ComparisonService> logger,
            SplDataService splDataService,
            IClaudeApiService claudeApiService,
            IOptions<MedRecPro.Models.ComparisonSettings> settings,
            IServiceProvider serviceProvider,
            ISplExportService splExportService)
        {
            #region implementation

            // Store injected dependencies for service operation
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _splDataService = splDataService ?? throw new ArgumentNullException(nameof(splDataService));
            _claudeApiService = claudeApiService ?? throw new ArgumentNullException(nameof(claudeApiService));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _splExportService = splExportService ?? throw new ArgumentNullException(nameof(splExportService));

            #endregion
        }

        #endregion

        #region public comparison methods

        /**************************************************************/
        /// <summary>
        /// Asynchronously generates a comprehensive AI-powered comparison analysis between SPL XML 
        /// and JSON representations, providing detailed assessment of data completeness, identified 
        /// discrepancies, and quantitative metrics for medical document validation.
        /// </summary>
        /// <param name="splDataGuid">
        /// The unique GUID identifier of the SPL data record to be analyzed. This GUID is used
        /// to retrieve the SPL data from the database including the original XML content.
        /// </param>
        /// <param name="additionalInstructions">
        /// Optional instructions to guide the AI analysis toward specific areas of concern such as
        /// clinical trial data, drug interactions, safety information, or regulatory compliance sections.
        /// </param>
        /// <returns>
        /// A task containing a ComparisonResponse with comprehensive analysis results including
        /// completeness assessment, detailed findings, identified issues, and quantitative metrics.
        /// </returns>
        /// <remarks>
        /// This method orchestrates the complete comparison workflow:
        /// 1. Retrieves SPL data from database using the provided GUID
        /// 2. Extracts XML content from the SPL data record
        /// 3. Obtains or generates corresponding JSON representation
        /// 4. Constructs AI-optimized comparison prompt with medical context
        /// 5. Invokes Claude AI service for intelligent analysis
        /// 6. Parses AI response into structured comparison results
        /// 
        /// The method ensures robust error handling and comprehensive logging throughout
        /// the process to support troubleshooting and operational monitoring.
        /// </remarks>
        /// <seealso cref="ComparisonResponse"/>
        /// <seealso cref="ComparisonResult"/>
        /// <seealso cref="IsSplDataReadyForComparisonAsync(Guid)"/>
        public async Task<ComparisonResponse> GenerateComparisonAsync(Guid splDataGuid, string? additionalInstructions = null)
        {
            #region implementation

            try
            {
                // Validate input parameter
                if (splDataGuid == Guid.Empty)
                {
                    throw new ArgumentException("SPL data GUID cannot be empty.", nameof(splDataGuid));
                }

                // Log the start of comparison operation for diagnostic purposes
                _logger.LogInformation("Starting comparison for SPL data GUID {SplDataGuid}", splDataGuid);

                var splData = await _splDataService.GetSplDataByGuidAsync(splDataGuid);

                if (splData == null)
                {
                    throw new InvalidOperationException($"SPL data not found for GUID: {splDataGuid}");
                }

                // Extract XML content from SPL data record
                var xmlContent = splData.SplXML;
                if (string.IsNullOrEmpty(xmlContent))
                {
                    throw new InvalidOperationException($"No XML content found in SPL data for GUID: {splDataGuid}");
                }

                // Obtain corresponding JSON representation (existing or generated)
                var jsonContent = await getOrGenerateJsonAsync(splData);

                // Construct AI-optimized prompt with medical document context
                var prompt = buildComparisonPrompt(xmlContent, jsonContent, additionalInstructions);

                // Invoke Claude AI service for intelligent comparison analysis
                var aiResponse = await _claudeApiService.GenerateDocumentComparisonAsync(prompt);

                // Parse AI response into structured comparison result object
                var result = await parseAiResponseAsync(aiResponse);

                // Construct comprehensive response object with analysis results
                var response = new ComparisonResponse
                {
                    FileId = splDataGuid.ToString(),
                    FileName = $"SPL-{splData.SplDataGUID}",
                    Result = result,
                    GeneratedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Successfully completed comparison for SPL data GUID {SplDataGuid}", splDataGuid);

                return response;
            }
            catch (Exception ex)
            {
                // Log comprehensive error information for troubleshooting
                _logger.LogError(ex, "Error generating comparison for SPL data GUID {SplDataGuid}", splDataGuid);
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asynchronously generates a comprehensive AI-powered comparison analysis between the original 
        /// SPL XML data and the structured DTO representation for a specific document. This method provides
        /// detailed assessment of data transformation accuracy, missing elements, and completeness metrics
        /// between the source XML and the processed Label entity structure.
        /// </summary>
        /// <param name="documentGuid">
        /// The unique GUID identifier of the document to analyze. This corresponds to the DocumentGUID 
        /// property in the Label.Document entity and is used to retrieve both the DTO structure and 
        /// locate the corresponding SPL XML source data.
        /// </param>
        /// <returns>
        /// A task containing a DocumentComparisonResult with comprehensive analysis results including
        /// completeness assessment, identified differences between XML and DTO, detailed findings,
        /// and quantitative metrics for data preservation validation.
        /// </returns>
        /// <remarks>
        /// This method extends the standard SPL comparison capabilities to focus specifically on 
        /// XML-to-DTO transformation analysis, which is critical for validating data integrity
        /// during the import and processing workflow. The analysis identifies:
        /// 
        /// - Missing data elements in the DTO that exist in the source XML
        /// - Structural differences between XML hierarchy and DTO relationships
        /// - Data accuracy issues during transformation processes
        /// - Completeness metrics for regulatory compliance validation
        /// 
        /// The method orchestrates the complete document comparison workflow including DTO retrieval,
        /// XML source location, content formatting, AI analysis, and result parsing into structured
        /// objects suitable for programmatic consumption and reporting.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when documentGuid is empty or invalid.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the document cannot be found, corresponding SPL data is unavailable,
        /// or AI analysis fails unexpectedly.
        /// </exception>
        /// <seealso cref="DocumentComparisonResult"/>
        /// <seealso cref="DocumentComparisonDifference"/>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="GenerateComparisonAsync(Guid, string)"/>
        public async Task<DocumentComparisonResult> GenerateDocumentComparisonAsync(Guid documentGuid)
        {
            #region implementation

            try
            {
                // Validate input parameter
                if (documentGuid == Guid.Empty)
                {
                    throw new ArgumentException("Document GUID cannot be empty.", nameof(documentGuid));
                }

                _logger.LogInformation("Starting document comparison analysis for GUID {DocumentGuid}", documentGuid);

                // Get the rendered SPL XML
                string xmlRendering = await _splExportService.ExportDocumentToSplAsync(documentGuid, minify:true);

                if (xmlRendering == null || !xmlRendering.Any())
                {
                    throw new InvalidOperationException($"Document with GUID {documentGuid} was not found.");
                }

                // Retrieve the SPL data and XML content
                var orginalSplData = await _splDataService.GetSplDataByGuidAsync(documentGuid);

                if (orginalSplData == null || string.IsNullOrEmpty(orginalSplData.SplXML))
                {
                    throw new InvalidOperationException($"SPL XML data not found for GUID: {documentGuid}");
                }

                // Build specialized prompt for document comparison
                var prompt = buildDocumentComparisonPrompt(orginalSplData.SplXML, xmlRendering);

                // Perform AI analysis
                var aiResponse = await _claudeApiService.GenerateDocumentComparisonAsync(prompt);

                // Parse AI response into document-specific result format
                var analysisResult = parseDocumentAnalysisResponse(aiResponse, documentGuid);

                _logger.LogInformation("Successfully completed document comparison analysis for GUID {DocumentGuid}", documentGuid);

                return analysisResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing document comparison analysis for GUID {DocumentGuid}", documentGuid);
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asynchronously determines whether a specified SPL data record is ready for comparison analysis
        /// by verifying the availability of required XML content in the database.
        /// </summary>
        /// <param name="splDataGuid">
        /// The unique GUID identifier of the SPL data record to check for comparison readiness.
        /// </param>
        /// <returns>
        /// A task containing a boolean value indicating SPL data readiness: true if the SPL data
        /// exists and contains XML content for comparison; false otherwise.
        /// </returns>
        /// <remarks>
        /// This method provides essential pre-validation before initiating resource-intensive
        /// comparison operations. It verifies SPL data availability and XML content presence to prevent
        /// failures during the comparison process and provides early feedback to client applications.
        /// 
        /// The readiness check ensures that the SPL data record exists in the database
        /// and contains the required XML content for analysis. JSON representation availability is checked
        /// during the comparison process itself, as it can be generated on-demand if needed.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when splDataGuid is empty or invalid.
        /// </exception>
        /// <seealso cref="GenerateComparisonAsync(Guid, string)"/>
        /// <seealso cref="SplDataService.GetSplDataByGuidAsync(Guid)"/>
        public async Task<bool> IsSplDataReadyForComparisonAsync(Guid splDataGuid)
        {
            #region implementation

            try
            {
                // Validate input parameter
                if (splDataGuid.IsNullOrEmpty())
                {
                    throw new ArgumentException("SPL data GUID cannot be empty.", nameof(splDataGuid));
                }

                var splData = await _splDataService.GetSplDataByGuidAsync(splDataGuid);

                return splData != null && !string.IsNullOrEmpty(splData.SplXML);
            }
            catch (Exception ex)
            {
                // Log readiness check errors and return false to indicate unavailability
                _logger.LogError(ex, "Error checking SPL data readiness for GUID {SplDataGuid}", splDataGuid);
                return false;
            }

            #endregion
        }

        #endregion

        #region private helper methods for standard comparison

        /**************************************************************/
        /// <summary>
        /// Asynchronously retrieves existing JSON content from SPL data or generates new JSON representation
        /// from XML content when no JSON version exists.
        /// </summary>
        /// <param name="splData">
        /// The SPL data record containing XML content and potentially existing JSON representation.
        /// </param>
        /// <returns>
        /// A task containing the JSON string representation, either retrieved from the SPL data
        /// or newly generated from the XML content.
        /// </returns>
        /// <remarks>
        /// This method optimizes performance by first checking for existing JSON representations
        /// in the SPL data before initiating potentially expensive XML-to-JSON conversion operations.
        /// Currently assumes JSON needs to be generated from XML as it's not stored in the SPL data model.
        /// </remarks>
        /// <seealso cref="convertXmlToJsonAsync(string)"/>
        private async Task<string> getOrGenerateJsonAsync(SplData splData)
        {
            #region implementation

            // Currently SPL data doesn't store JSON, so we need to generate it
            // In the future, if JSON is stored in the SPL data model, check for existing JSON first

            _logger.LogInformation("Generating JSON from XML for SPL data GUID {SplDataGuid}", splData.SplDataGUID);

            // Convert XML to JSON using configured conversion logic
            var jsonContent = await convertXmlToJsonAsync(splData.SplXML);

            return jsonContent;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Constructs a comprehensive AI-optimized prompt for comparing SPL XML and JSON content,
        /// incorporating medical document context and specific analysis instructions.
        /// </summary>
        /// <param name="xmlContent">
        /// The original SPL XML content to be compared.
        /// </param>
        /// <param name="jsonContent">
        /// The JSON representation to be validated against the XML source.
        /// </param>
        /// <param name="additionalInstructions">
        /// Optional specific instructions to guide AI analysis focus areas.
        /// </param>
        /// <returns>
        /// A structured prompt string optimized for Claude AI analysis containing
        /// analysis instructions, content comparison requirements, and the document content.
        /// </returns>
        /// <remarks>
        /// The prompt construction follows best practices for AI interaction, providing:
        /// - Clear analysis objectives and success criteria
        /// - Structured output format requirements
        /// - Medical document context and terminology
        /// - Specific content sections for systematic comparison
        /// 
        /// This method ensures consistent prompt formatting and comprehensive analysis
        /// coverage for reliable AI-powered comparison results.
        /// </remarks>
        private string buildComparisonPrompt(string xmlContent, string jsonContent, string? additionalInstructions)
        {
            #region implementation

            // Build comprehensive prompt with structured analysis requirements
            var prompt = new StringBuilder();
            prompt.AppendLine("Please compare the XML input and JSON output to determine if all the data in the XML is represented in the JSON.");
            prompt.AppendLine();
            prompt.AppendLine("Provide a structured analysis including:");
            prompt.AppendLine("1. Overall completeness assessment (Complete/Incomplete)");
            prompt.AppendLine("2. Summary of findings");
            prompt.AppendLine("3. Detailed section-by-section analysis");
            prompt.AppendLine("4. List any missing or mismatched data");
            prompt.AppendLine("5. Provide completion metrics");
            prompt.AppendLine();

            // Include additional focus areas if specified by the user
            if (!string.IsNullOrEmpty(additionalInstructions))
            {
                prompt.AppendLine("Additional Instructions:");
                prompt.AppendLine(additionalInstructions);
                prompt.AppendLine();
            }

            // Append content sections for AI analysis
            prompt.AppendLine("XML Content:");
            prompt.AppendLine(xmlContent);
            prompt.AppendLine();
            prompt.AppendLine("JSON Content:");
            prompt.AppendLine(jsonContent);

            return prompt.ToString();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asynchronously parses AI-generated comparison analysis into structured ComparisonResult
        /// objects, extracting completeness assessment, issues, and quantitative metrics.
        /// </summary>
        /// <param name="aiResponse">
        /// The raw text response from the Claude AI service containing comparison analysis.
        /// </param>
        /// <returns>
        /// A task containing a structured ComparisonResult with parsed completeness status,
        /// summary information, detailed analysis, identified issues, and calculated metrics.
        /// </returns>
        /// <remarks>
        /// This parsing method employs pattern recognition and text analysis to extract
        /// structured information from natural language AI responses. It identifies:
        /// - Completeness indicators and status markers
        /// - Summary statements and key findings
        /// - Issue descriptions and severity levels
        /// - Quantitative metrics and section counts
        /// 
        /// The parser is designed to be resilient to variations in AI response formatting
        /// while maintaining accuracy in data extraction.
        /// </remarks>
        /// <seealso cref="extractIssuesFromResponse(string)"/>
        /// <seealso cref="extractMetricsFromResponse(string)"/>
        /// <seealso cref="ComparisonResult"/>
        private async Task<ComparisonResult> parseAiResponseAsync(string aiResponse)
        {
            #region implementation

            // Initialize structured result object with raw AI analysis
            var result = new ComparisonResult
            {
                DetailedAnalysis = aiResponse
            };

            // Extract completeness status from AI response patterns
            result.IsComplete = aiResponse.Contains("COMPLETE", StringComparison.OrdinalIgnoreCase) ||
                               aiResponse.Contains("✅ COMPLETE", StringComparison.OrdinalIgnoreCase);

            // Extract summary information from response structure
            var lines = aiResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            result.Summary = lines.FirstOrDefault()?.Trim() ?? "Analysis completed";

            // Parse specific issues and quantitative metrics from AI response
            result.Issues = extractIssuesFromResponse(aiResponse);
            result.Metrics = extractMetricsFromResponse(aiResponse);

            return result;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts specific comparison issues from AI response text, identifying missing data,
        /// mismatches, and other discrepancies with appropriate severity classification.
        /// </summary>
        /// <param name="response">
        /// The AI-generated response text containing issue descriptions and findings.
        /// </param>
        /// <returns>
        /// A list of ComparisonIssue objects with classified problem types, descriptions,
        /// and severity levels extracted from the AI analysis.
        /// </returns>
        /// <remarks>
        /// This method uses pattern recognition to identify common issue indicators
        /// in AI responses such as missing data markers, error symbols, and problem
        /// description patterns. Issues are automatically classified by type and
        /// assigned appropriate severity levels for prioritization.
        /// </remarks>
        /// <seealso cref="ComparisonIssue"/>
        private List<ComparisonIssue> extractIssuesFromResponse(string response)
        {
            #region implementation

            var issues = new List<ComparisonIssue>();

            // Scan response lines for issue indicators and problem patterns
            var lines = response.Split('\n');
            foreach (var line in lines)
            {
                // Identify lines containing issue markers or problem descriptions
                if (line.Contains("❌") || line.Contains("Missing", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Issue", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ComparisonIssue
                    {
                        Type = "Missing",
                        Description = line.Trim(),
                        Severity = "Medium"
                    });
                }
            }

            return issues;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts quantitative metrics from AI response text, calculating completion
        /// percentages, section counts, and other measurable comparison statistics.
        /// </summary>
        /// <param name="response">
        /// The AI-generated response containing analysis results and metrics information.
        /// </param>
        /// <returns>
        /// A ComparisonMetrics object containing calculated completion percentage,
        /// total sections, complete sections, and derived statistics.
        /// </returns>
        /// <remarks>
        /// This method analyzes AI response patterns to extract quantifiable data
        /// about the comparison results, providing objective measurements to complement
        /// the qualitative analysis. Metrics are derived from completion indicators,
        /// section counts, and success markers in the response text.
        /// </remarks>
        /// <seealso cref="ComparisonMetrics"/>
        /// <seealso cref="countSections(string)"/>
        /// <seealso cref="countCompleteSections(string)"/>
        private ComparisonMetrics extractMetricsFromResponse(string response)
        {
            #region implementation

            // Calculate quantitative metrics from AI response analysis
            var totalSections = countSections(response);
            var completeSections = countCompleteSections(response);

            return new ComparisonMetrics
            {
                CompletionPercentage = response.Contains("COMPLETE", StringComparison.OrdinalIgnoreCase) ? 100.0 : 75.0,
                TotalSections = totalSections,
                CompleteSections = completeSections,
                MissingSections = Math.Max(0, totalSections - completeSections)
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Counts the total number of sections identified in the AI response analysis
        /// by scanning for section delimiter patterns and structural markers.
        /// </summary>
        /// <param name="response">
        /// The AI response text to analyze for section count extraction.
        /// </param>
        /// <returns>
        /// The total number of sections identified in the comparison analysis.
        /// </returns>
        /// <seealso cref="extractMetricsFromResponse(string)"/>
        private int countSections(string response)
        {
            #region implementation

            // Count section delimiters to determine total sections analyzed
            return response.Split("Section", StringSplitOptions.RemoveEmptyEntries).Length - 1;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Counts the number of complete sections identified in the AI response
        /// by scanning for completion markers and success indicators.
        /// </summary>
        /// <param name="response">
        /// The AI response text to analyze for complete section markers.
        /// </param>
        /// <returns>
        /// The number of sections marked as complete in the analysis.
        /// </returns>
        /// <seealso cref="extractMetricsFromResponse(string)"/>
        private int countCompleteSections(string response)
        {
            #region implementation

            // Count completion markers to determine successful section validations
            return response.Split("✅", StringSplitOptions.RemoveEmptyEntries).Length - 1;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asynchronously converts SPL XML content to JSON format using configured
        /// conversion logic and transformation rules for medical document processing.
        /// </summary>
        /// <param name="xmlContent">
        /// The original SPL XML content to be converted to JSON representation.
        /// </param>
        /// <returns>
        /// A task containing the JSON string representation of the XML content.
        /// </returns>
        /// <remarks>
        /// This method placeholder should be replaced with actual XML to JSON conversion
        /// logic specific to SPL document structure and medical data requirements.
        /// The conversion should preserve all medical information including drug safety
        /// data, clinical trial results, and regulatory compliance details.
        /// </remarks>
        /// <exception cref="NotImplementedException">
        /// Currently thrown as this method requires implementation of specific
        /// XML to JSON conversion logic for SPL documents.
        /// </exception>
        /// <seealso cref="getOrGenerateJsonAsync(SplData)"/>
        private async Task<string> convertXmlToJsonAsync(string xmlContent)
        {
            #region implementation

            // TODO: Implement SPL-specific XML to JSON conversion logic
            // This should integrate with existing conversion services or libraries
            // to ensure accurate preservation of medical document structure and content
            throw new NotImplementedException("Implement XML to JSON conversion");

            #endregion
        }

        #endregion

        #region private helper methods for document comparison
   
        /**************************************************************/
        /// <summary>
        /// Constructs a comprehensive AI-optimized prompt for comparing SPL document XML source
        /// with DTO XML Rendering representation, requesting structured XML Rendering response for enhanced
        /// data processing and user interface integration.
        /// </summary>
        /// <param name="xmlContent">The original SPL XML source content to be analyzed.</param>
        /// <param name="xmlRendering">The DTO XML Rendering representation to compare against the Source XML.</param>
        /// <returns>
        /// A structured prompt string optimized for Claude AI analysis that requests XML Rendering output
        /// matching the DocumentComparisonResult model structure for seamless integration.
        /// </returns>
        /// <remarks>
        /// The prompt construction follows medical document analysis best practices while requesting
        /// structured XML Rendering output instead of markdown text, providing:
        /// - Clear analysis objectives for SPL data comparison with XML Rendering response format
        /// - Structured output schema matching DocumentComparisonResult model
        /// - Medical terminology context for accurate pharmaceutical analysis
        /// - Specific focus areas for regulatory compliance validation
        /// - XML Rendering-only response requirements for improved parsing and display
        /// </remarks>
        /// <seealso cref="DocumentComparisonResult"/>
        private string buildDocumentComparisonPrompt(string xmlContent, string xmlRendering)
        {
            #region implementation

            var prompt = new StringBuilder();

            prompt.AppendLine("# SPL Document XML-to-DTO Transformation Analysis");
            prompt.AppendLine();
            prompt.AppendLine("Analyze the Source XML and XML Rendering below and provide ONLY a JSON response with the exact structure shown.");
            prompt.AppendLine();
            prompt.AppendLine("**CRITICAL: Your response must be ONLY the JSON object below. No explanations, no markdown, no additional text.**");
            prompt.AppendLine();
            prompt.AppendLine("Required XML Rendering structure:");
            prompt.AppendLine("{");
            prompt.AppendLine("  \"documentGuid\": \"240fa4f4-d357-9079-e063-6394a90a77e2\",");
            prompt.AppendLine("  \"generatedAt\": \"2025-08-12T17:45:00.000Z\",");
            prompt.AppendLine("  \"isComplete\": true,");
            prompt.AppendLine("  \"completionPercentage\": 95.0,");
            prompt.AppendLine("  \"summary\": \"Brief one-sentence summary of findings\",");
            prompt.AppendLine("  \"detailedAnalysis\": [");
            prompt.AppendLine("    \"Overall Assessment: [Your analysis of overall transformation quality]\",");
            prompt.AppendLine("    \"Completeness Assessment: [Analysis of data completeness]\",");
            prompt.AppendLine("    \"Structural Integrity: [Analysis of hierarchical preservation]\",");
            prompt.AppendLine("    \"Data Accuracy: [Analysis of data precision]\",");
            prompt.AppendLine("    \"Medical Content Validation: [Analysis of pharmaceutical content]\",");
            prompt.AppendLine("    \"Regulatory Compliance: [Analysis of FDA requirements]\",");
            prompt.AppendLine("    \"Impact Assessment: [Analysis of any issues found]\",");
            prompt.AppendLine("    \"Conclusion: [Final recommendation and approval status]\"");
            prompt.AppendLine("  ],");
            prompt.AppendLine("  \"differences\": [");
            prompt.AppendLine("    {");
            prompt.AppendLine("      \"type\": \"Missing|Mismatch|Structural|General\",");
            prompt.AppendLine("      \"section\": \"Specific section name\",");
            prompt.AppendLine("      \"severity\": \"Critical|High|Medium|Low\",");
            prompt.AppendLine("      \"description\": \"Brief description of this specific issue\"");
            prompt.AppendLine("    }");
            prompt.AppendLine("  ]");
            prompt.AppendLine("}");
            prompt.AppendLine();
            prompt.AppendLine("Requirements:");
            prompt.AppendLine("- Each detailedAnalysis array element should be a complete paragraph");
            prompt.AppendLine("- Start each element with the section name followed by colon");
            prompt.AppendLine("- Compare ALL sections between XML Source and XML Rendering");
            prompt.AppendLine("- Check if ingredient data, product info, warnings, dosage, etc. are preserved");
            prompt.AppendLine("- Each difference should be a specific issue, not entire analysis");
            prompt.AppendLine("- Provide completion percentage 0-100 based on data preservation");
            prompt.AppendLine();
            prompt.AppendLine("Documents to compare:");
            prompt.AppendLine();
            prompt.AppendLine("XML Source:");
            prompt.AppendLine(xmlContent);
            prompt.AppendLine();
            prompt.AppendLine("XML Rendering:");
            prompt.AppendLine(xmlRendering);

            var promptText = prompt.ToString();

            if (promptText.Length > _settings.MaxPromptLength)
            {
                _logger.LogWarning("Generated prompt length ({Length}) exceeds configured maximum ({MaxLength})",
                    promptText.Length, _settings.MaxPromptLength);
                throw new ArgumentException($"Prompt length ({promptText.Length}) exceeds maximum allowed ({_settings.MaxPromptLength})");
            }

            return promptText;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the AI-generated JSON comparison analysis response into a structured DocumentComparisonResult
        /// object, handling both JSON-structured responses and fallback text parsing for legacy compatibility.
        /// </summary>
        /// <param name="aiResponse">The JSON or text response from Claude AI containing comparison analysis.</param>
        /// <param name="documentGuid">The document GUID being analyzed for result association.</param>
        /// <returns>
        /// A structured DocumentComparisonResult containing parsed completeness status,
        /// difference analysis, metrics, and detailed findings.
        /// </returns>
        /// <remarks>
        /// This parsing method first attempts to parse structured JSON responses from Claude AI.
        /// If JSON parsing fails, it falls back to text pattern recognition for compatibility
        /// with legacy response formats, ensuring robust handling of various AI response types.
        /// </remarks>
        /// <seealso cref="DocumentComparisonResult"/>
        /// <seealso cref="DocumentComparisonDifference"/>
        private DocumentComparisonResult parseDocumentAnalysisResponse(string aiResponse, Guid documentGuid)
        {
            #region implementation
            try
            {
                // Clean the response of any markdown or extra formatting
                string cleanedResponse = cleanJsonResponse(aiResponse);

                _logger.LogDebug("Attempting to parse Claude response for document {DocumentGuid}", documentGuid);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var result = JsonSerializer.Deserialize<DocumentComparisonResult>(cleanedResponse, options);

                if (result == null)
                {
                    _logger.LogWarning("JSON deserialization returned null for document {DocumentGuid}", documentGuid);
                    return createFallbackResult(aiResponse, documentGuid);
                }

                // Ensure required fields are set correctly
                result.DocumentGuid = documentGuid;
                result.GeneratedAt = DateTime.UtcNow;

                // Clean up any malformed data
                result = cleanupParsedResult(result);

                _logger.LogInformation("Successfully parsed JSON response for document {DocumentGuid}. " +
                                     "Completion: {IsComplete}, Percentage: {Percentage}, Differences: {DifferenceCount}",
                    documentGuid, result.IsComplete, result.CompletionPercentage, result.Differences.Count);

                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse JSON response for document {DocumentGuid}. Response: {Response}",
                    documentGuid, aiResponse.Substring(0, Math.Min(500, aiResponse.Length)));

                return createFallbackResult(aiResponse, documentGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error parsing response for document {DocumentGuid}", documentGuid);
                return createFallbackResult(aiResponse, documentGuid);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Cleans the Claude AI response to extract valid JSON, removing markdown formatting
        /// and other artifacts that might interfere with JSON parsing.
        /// </summary>
        private string cleanJsonResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return "{}";

            // Remove markdown code blocks
            response = response.Replace("```json", "").Replace("```", "");

            // Find the JSON object boundaries
            var startIndex = response.IndexOf('{');
            var lastIndex = response.LastIndexOf('}');

            if (startIndex >= 0 && lastIndex > startIndex)
            {
                response = response.Substring(startIndex, lastIndex - startIndex + 1);
            }

            return response.Trim();
        }

        /**************************************************************/
        /// <summary>
        /// Cleans up parsed results to fix common issues from Claude responses.
        /// </summary>
        private DocumentComparisonResult cleanupParsedResult(DocumentComparisonResult result)
        {
            // Fix summary if it contains JSON syntax
            if (result.Summary.Contains("\"summary\":"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    result.Summary,
                    "\"summary\":\\s*\"([^\"]+)\"");
                if (match.Success)
                {
                    result.Summary = match.Groups[1].Value;
                }
            }

            // Clean up detailedAnalysis list - remove any malformed entries
            if (result.DetailedAnalysis != null)
            {
                result.DetailedAnalysis = result.DetailedAnalysis
                    .Where(item => !string.IsNullOrWhiteSpace(item) &&
                                  item.Length > 10 &&
                                  !item.Contains("```json") &&
                                  !item.StartsWith("{"))
                    .Select(item => item.Trim())
                    .ToList();
            }
            else
            {
                result.DetailedAnalysis = new List<string>();
            }

            // Filter out malformed differences
            result.Differences = result.Differences
                .Where(d => !string.IsNullOrWhiteSpace(d.Description) &&
                           !d.Description.Contains("\"detailedAnalysis\":") &&
                           d.Description.Length < 1000) // Reasonable length limit
                .ToList();

            // Ensure completion percentage is within valid range
            if (result.CompletionPercentage < 0) result.CompletionPercentage = 0;
            if (result.CompletionPercentage > 100) result.CompletionPercentage = 100;

            return result;
        }

        /**************************************************************/
        /// <summary>
        /// Creates a fallback result when JSON parsing fails, using text pattern matching.
        /// </summary>
        private DocumentComparisonResult createFallbackResult(string aiResponse, Guid documentGuid)
        {
            return new DocumentComparisonResult
            {
                DocumentGuid = documentGuid,
                GeneratedAt = DateTime.UtcNow,
                IsComplete = extractCompletionStatusFromText(aiResponse),
                CompletionPercentage = extractCompletionPercentageFromText(aiResponse),
                Summary = extractSummaryFromText(aiResponse),
                DetailedAnalysis = convertTextToAnalysisList(aiResponse),
                Differences = extractDifferencesFromText(aiResponse)
            };
        }

        /**************************************************************/
        /// <summary>
        /// Converts a text response to a structured analysis list by splitting on section headers.
        /// </summary>
        private List<string> convertTextToAnalysisList(string text)
        {
            var analysisList = new List<string>();

            // Split text into sections based on common headers
            var sectionHeaders = new[]
            {
                "Overall Assessment:",
                "Completeness Assessment:",
                "Structural Integrity:",
                "Data Accuracy:",
                "Medical Content Validation:",
                "Regulatory Compliance:",
                "Impact Assessment:",
                "Conclusion:"
            };

            var sections = new List<string>();
            var currentSection = new StringBuilder();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Check if this line starts a new section
                if (sectionHeaders.Any(header => trimmedLine.StartsWith(header, StringComparison.OrdinalIgnoreCase)))
                {
                    // Save previous section if it has content
                    if (currentSection.Length > 0)
                    {
                        sections.Add(currentSection.ToString().Trim());
                        currentSection.Clear();
                    }

                    // Start new section
                    currentSection.AppendLine(trimmedLine);
                }
                else if (currentSection.Length > 0)
                {
                    // Continue current section
                    currentSection.AppendLine(trimmedLine);
                }
                else if (trimmedLine.Length > 20) // Standalone content
                {
                    sections.Add(trimmedLine);
                }
            }

            // Add final section
            if (currentSection.Length > 0)
            {
                sections.Add(currentSection.ToString().Trim());
            }

            // If no structured sections found, split by double line breaks
            if (sections.Count == 0)
            {
                sections = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                              .Where(s => s.Trim().Length > 20)
                              .ToList();
            }

            return sections;
        }

        /**************************************************************/
        /// <summary>
        /// Extracts completion status from text using pattern matching.
        /// </summary>
        private bool extractCompletionStatusFromText(string text)
        {
            return text.Contains("COMPLETE", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("\"isComplete\": true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Extracts completion percentage from text using pattern matching.
        /// </summary>
        private double extractCompletionPercentageFromText(string text)
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)%");
            if (match.Success && double.TryParse(match.Groups[1].Value, out double percentage))
            {
                return percentage;
            }

            var jsonMatch = System.Text.RegularExpressions.Regex.Match(text, @"""completionPercentage"":\s*(\d+\.?\d*)");
            if (jsonMatch.Success && double.TryParse(jsonMatch.Groups[1].Value, out double jsonPercentage))
            {
                return jsonPercentage;
            }

            return 0;
        }

        /**************************************************************/
        /// <summary>
        /// Extracts summary from text, taking the first meaningful sentence.
        /// </summary>
        private string extractSummaryFromText(string text)
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return lines.FirstOrDefault(l => l.Length > 20 && !l.StartsWith("#"))?.Trim()
                   ?? "Analysis completed";
        }

        /// <summary>
        /// Extracts differences from text using pattern matching.
        /// </summary>
        private List<DocumentComparisonDifference> extractDifferencesFromText(string text)
        {
            // Return empty list for text fallback - this would need more sophisticated parsing
            return new List<DocumentComparisonDifference>();
        }

        #endregion
    }

    #endregion
}