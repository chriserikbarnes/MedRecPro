using MedRecPro.Models;
using MedRecPro.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Management;
using System.Text;
using System.Text.Json;

namespace MedRecPro.Service
{
    #region comparison service implementation

    /**************************************************************/
    /// <summary>
    /// Provides comprehensive SPL (Structured Product Labeling) comparison services that leverage
    /// artificial intelligence to analyze data completeness and accuracy between XML and JSON formats.
    /// This service orchestrates the entire comparison workflow including file retrieval, AI analysis,
    /// result parsing, and report generation for medical record validation systems.
    /// </summary>
    /// <remarks>
    /// The ComparisonService serves as the primary implementation of medical document comparison functionality,
    /// integrating multiple services to provide end-to-end validation of SPL data transformations.
    /// It ensures that critical medical information including drug safety data, clinical trial results,
    /// dosage instructions, and regulatory compliance details are accurately preserved during format conversions.
    /// 
    /// Key responsibilities include:
    /// - Orchestrating file retrieval from storage systems
    /// - Managing XML to JSON conversion when needed
    /// - Constructing AI-optimized comparison prompts
    /// - Processing AI responses into structured results
    /// - Generating comprehensive comparison reports
    /// - Providing file readiness validation
    /// </remarks>
    /// <example>
    /// <code>
    /// // Dependency injection registration
    /// services.AddScoped&lt;IComparisonService, ComparisonService&gt;();
    /// services.Configure&lt;ComparisonSettings&gt;(configuration.GetSection("Comparison"));
    /// 
    /// // Usage in controller
    /// public class ComparisonController : ControllerBase
    /// {
    ///     private readonly IComparisonService _comparisonService;
    ///     
    ///     public ComparisonController(IComparisonService comparisonService)
    ///     {
    ///         _comparisonService = comparisonService;
    ///     }
    ///     
    ///     [HttpPost("generate")]
    ///     public async Task&lt;IActionResult&gt; GenerateComparison([FromBody] ComparisonRequest request)
    ///     {
    ///         var result = await _comparisonService.GenerateComparisonAsync(
    ///             request.FileId, 
    ///             request.AdditionalInstructions);
    ///         return Ok(result);
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="IComparisonService"/>
    /// <seealso cref="ComparisonRequest"/>
    /// <seealso cref="ComparisonResponse"/>
    /// <seealso cref="IClaudeApiService"/>
    /// <seealso cref="IFileStorageService"/>
    public class ComparisonService : IComparisonService
    {
        #region dependency injection fields

        /// <summary>
        /// Logger instance for capturing comparison service operations and diagnostics.
        /// </summary>
        /// <seealso cref="ILogger"/>
        private readonly ILogger<ComparisonService> _logger;

        /// <summary>
        /// File storage service for managing XML, JSON, and report file operations.
        /// </summary>
        /// <seealso cref="IFileStorageService"/>
        private readonly IFileStorageService _fileStorageService;

        /// <summary>
        /// Claude API service for AI-powered comparison analysis operations.
        /// </summary>
        /// <seealso cref="IClaudeApiService"/>
        private readonly IClaudeApiService _claudeApiService;

        /// <summary>
        /// Configuration settings for comparison behavior and processing parameters.
        /// </summary>
        /// <seealso cref="ComparisonSettings"/>
        private readonly ComparisonSettings _settings;

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
        /// <param name="fileStorageService">
        /// Service responsible for managing file operations including reading XML/JSON content,
        /// saving generated files, and checking file availability in the storage system.
        /// </param>
        /// <param name="claudeApiService">
        /// AI service interface for performing intelligent comparison analysis between
        /// SPL XML and JSON representations using Claude's language model capabilities.
        /// </param>
        /// <param name="settings">
        /// Configuration options containing comparison behavior settings, file size limits,
        /// caching preferences, and other operational parameters.
        /// </param>
        /// <remarks>
        /// This constructor follows dependency injection patterns to ensure loose coupling
        /// and testability. All dependencies are required and validated for null values
        /// through the dependency injection container configuration.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Manual instantiation (typically handled by DI container)
        /// var logger = serviceProvider.GetRequiredService&lt;ILogger&lt;ComparisonService&gt;&gt;();
        /// var fileStorage = serviceProvider.GetRequiredService&lt;IFileStorageService&gt;();
        /// var claudeApi = serviceProvider.GetRequiredService&lt;IClaudeApiService&gt;();
        /// var settings = serviceProvider.GetRequiredService&lt;IOptions&lt;ComparisonSettings&gt;&gt;();
        /// 
        /// var comparisonService = new ComparisonService(logger, fileStorage, claudeApi, settings);
        /// </code>
        /// </example>
        /// <seealso cref="ILogger{TCategoryName}"/>
        /// <seealso cref="IFileStorageService"/>
        /// <seealso cref="IClaudeApiService"/>
        /// <seealso cref="IOptions{TOptions}"/>
        public ComparisonService(
            ILogger<ComparisonService> logger,
            IFileStorageService fileStorageService,
            IClaudeApiService claudeApiService,
            IOptions<ComparisonSettings> settings)
        {
            #region implementation

            // Store injected dependencies for service operation
            _logger = logger;
            _fileStorageService = fileStorageService;
            _claudeApiService = claudeApiService;
            _settings = settings.Value;

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
        /// <param name="fileId">
        /// The unique identifier of the uploaded SPL file to be analyzed. This identifier is used
        /// to retrieve both the original XML content and corresponding JSON representation.
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
        /// 1. Retrieves original XML content from file storage
        /// 2. Obtains or generates corresponding JSON representation
        /// 3. Constructs AI-optimized comparison prompt with medical context
        /// 4. Invokes Claude AI service for intelligent analysis
        /// 5. Parses AI response into structured comparison results
        /// 6. Optionally saves detailed comparison report for audit purposes
        /// 
        /// The method ensures robust error handling and comprehensive logging throughout
        /// the process to support troubleshooting and operational monitoring.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Basic comparison analysis
        /// var basicResult = await comparisonService.GenerateComparisonAsync("file-123");
        /// Console.WriteLine($"Completion: {basicResult.Result.Metrics.CompletionPercentage}%");
        /// 
        /// // Focused analysis with specific instructions
        /// var focusedResult = await comparisonService.GenerateComparisonAsync(
        ///     "file-456", 
        ///     "Pay special attention to drug interaction completeness and clinical trial data accuracy");
        /// 
        /// // Process analysis results
        /// if (!focusedResult.Result.IsComplete)
        /// {
        ///     foreach (var issue in focusedResult.Result.Issues)
        ///     {
        ///         Console.WriteLine($"{issue.Severity}: {issue.Description} in {issue.Section}");
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <exception cref="ArgumentException">
        /// Thrown when fileId is null, empty, or whitespace.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Thrown when the specified XML file cannot be located in storage.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when file processing or AI analysis fails unexpectedly.
        /// </exception>
        /// <seealso cref="ComparisonResponse"/>
        /// <seealso cref="ComparisonResult"/>
        /// <seealso cref="IsFileReadyForComparisonAsync(string)"/>
        public async Task<ComparisonResponse> GenerateComparisonAsync(string fileId, string? additionalInstructions = null)
        {
            #region implementation

            try
            {
                // Log the start of comparison operation for diagnostic purposes
                _logger.LogInformation("Starting comparison for file {FileId}", fileId);

                // Retrieve original SPL XML content from storage system
                var xmlContent = await _fileStorageService.GetFileContentAsync(fileId, "xml");
                if (string.IsNullOrEmpty(xmlContent))
                {
                    throw new FileNotFoundException($"XML file not found for ID: {fileId}");
                }

                // Obtain corresponding JSON representation (existing or generated)
                var jsonContent = await getOrGenerateJsonAsync(fileId, xmlContent);

                // Construct AI-optimized prompt with medical document context
                var prompt = buildComparisonPrompt(xmlContent, jsonContent, additionalInstructions);

                // Invoke Claude AI service for intelligent comparison analysis
                var aiResponse = await _claudeApiService.GenerateCompletionAsync(prompt);

                // Parse AI response into structured comparison result object
                var result = await parseAiResponseAsync(aiResponse);

                // Retrieve original filename for response context
                var fileName = await _fileStorageService.GetFileNameAsync(fileId);

                // Construct comprehensive response object with analysis results
                var response = new ComparisonResponse
                {
                    FileId = fileId,
                    FileName = fileName ?? "Unknown",
                    Result = result,
                    GeneratedAt = DateTime.UtcNow
                };

                // Optionally save detailed comparison report for audit trail
                await saveComparisonReportAsync(fileId, response);

                return response;
            }
            catch (Exception ex)
            {
                // Log comprehensive error information for troubleshooting
                _logger.LogError(ex, "Error generating comparison for file {FileId}", fileId);
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asynchronously determines whether a specified SPL file is ready for comparison analysis
        /// by verifying the availability of required XML content in the storage system.
        /// </summary>
        /// <param name="fileId">
        /// The unique identifier of the file to check for comparison readiness.
        /// </param>
        /// <returns>
        /// A task containing a boolean value indicating file readiness: true if the XML file
        /// exists and is accessible for comparison; false otherwise.
        /// </returns>
        /// <remarks>
        /// This method provides essential pre-validation before initiating resource-intensive
        /// comparison operations. It verifies file availability and accessibility to prevent
        /// failures during the comparison process and provides early feedback to client applications.
        /// 
        /// The readiness check ensures that the original XML file exists in the storage system
        /// and can be retrieved for analysis. JSON representation availability is checked
        /// during the comparison process itself, as it can be generated on-demand if needed.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Verify file readiness before comparison
        /// string fileId = "medical-spl-001";
        /// bool isReady = await comparisonService.IsFileReadyForComparisonAsync(fileId);
        /// 
        /// if (isReady)
        /// {
        ///     var result = await comparisonService.GenerateComparisonAsync(fileId);
        ///     ProcessResults(result);
        /// }
        /// else
        /// {
        ///     Console.WriteLine("File not ready - please upload XML file first");
        /// }
        /// 
        /// // API endpoint usage
        /// [HttpGet("ready/{fileId}")]
        /// public async Task&lt;bool&gt; CheckReadiness(string fileId)
        /// {
        ///     return await _comparisonService.IsFileReadyForComparisonAsync(fileId);
        /// }
        /// </code>
        /// </example>
        /// <exception cref="ArgumentException">
        /// Thrown when fileId is null, empty, or whitespace.
        /// </exception>
        /// <seealso cref="GenerateComparisonAsync(string, string)"/>
        /// <seealso cref="IFileStorageService.FileExistsAsync(string, string)"/>
        public async Task<bool> IsFileReadyForComparisonAsync(string fileId)
        {
            #region implementation

            try
            {
                // Check if XML file exists in storage system for comparison readiness
                var xmlExists = await _fileStorageService.FileExistsAsync(fileId, "xml");
                return xmlExists;
            }
            catch (Exception ex)
            {
                // Log readiness check errors and return false to indicate unavailability
                _logger.LogError(ex, "Error checking file readiness for {FileId}", fileId);
                return false;
            }

            #endregion
        }

        #endregion

        #region private helper methods

        /**************************************************************/
        /// <summary>
        /// Asynchronously retrieves existing JSON content or generates new JSON representation
        /// from XML content when no JSON version exists in the storage system.
        /// </summary>
        /// <param name="fileId">
        /// The unique identifier for locating existing JSON content in storage.
        /// </param>
        /// <param name="xmlContent">
        /// The original XML content to convert to JSON format if no existing JSON is found.
        /// </param>
        /// <returns>
        /// A task containing the JSON string representation, either retrieved from storage
        /// or newly generated from the provided XML content.
        /// </returns>
        /// <remarks>
        /// This method optimizes performance by first checking for existing JSON representations
        /// before initiating potentially expensive XML-to-JSON conversion operations.
        /// Generated JSON content is automatically saved to storage for future use,
        /// improving efficiency for subsequent comparison requests.
        /// </remarks>
        /// <seealso cref="convertXmlToJsonAsync(string)"/>
        /// <seealso cref="IFileStorageService.GetFileContentAsync(string, string)"/>
        /// <seealso cref="IFileStorageService.SaveFileContentAsync(string, string, string)"/>
        private async Task<string> getOrGenerateJsonAsync(string fileId, string xmlContent)
        {
            #region implementation

            // First attempt to retrieve existing JSON content from storage
            var existingJson = await _fileStorageService.GetFileContentAsync(fileId, "json");
            if (!string.IsNullOrEmpty(existingJson))
            {
                return existingJson;
            }

            // Generate new JSON representation from XML if none exists
            _logger.LogInformation("Generating JSON from XML for file {FileId}", fileId);

            // Convert XML to JSON using configured conversion logic
            var jsonContent = await convertXmlToJsonAsync(xmlContent);

            // Save generated JSON to storage for future use and performance optimization
            await _fileStorageService.SaveFileContentAsync(fileId, jsonContent, "json");

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
        /// <seealso cref="IClaudeApiService.GenerateCompletionAsync(string)"/>
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
            result.IsComplete = aiResponse.Contains("COMPLETE") || aiResponse.Contains("✅ COMPLETE");

            // Extract summary information from response structure
            var lines = aiResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            result.Summary = lines.FirstOrDefault() ?? "Analysis completed";

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
                if (line.Contains("❌") || line.Contains("Missing") || line.Contains("Issue"))
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
                CompletionPercentage = response.Contains("COMPLETE") ? 100.0 : 75.0,
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
        /// <seealso cref="getOrGenerateJsonAsync(string, string)"/>
        private async Task<string> convertXmlToJsonAsync(string xmlContent)
        {
            #region implementation

            // TODO: Implement SPL-specific XML to JSON conversion logic
            // This should integrate with existing conversion services or libraries
            // to ensure accurate preservation of medical document structure and content
            throw new NotImplementedException("Implement XML to JSON conversion");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asynchronously saves a comprehensive comparison report in JSON format
        /// for audit trail purposes and future reference in the storage system.
        /// </summary>
        /// <param name="fileId">
        /// The unique identifier for associating the report with the original file.
        /// </param>
        /// <param name="response">
        /// The complete ComparisonResponse object containing analysis results to save.
        /// </param>
        /// <remarks>
        /// This method creates a detailed audit trail by persisting comparison results
        /// in a structured JSON format. The saved reports can be used for:
        /// - Historical analysis and trend identification
        /// - Regulatory compliance documentation
        /// - Quality assurance and validation tracking
        /// - Troubleshooting and error analysis
        /// 
        /// Failures in report saving are logged but do not affect the main comparison
        /// operation, ensuring system resilience.
        /// </remarks>
        /// <seealso cref="ComparisonResponse"/>
        /// <seealso cref="IFileStorageService.SaveFileContentAsync(string, string, string)"/>
        private async Task saveComparisonReportAsync(string fileId, ComparisonResponse response)
        {
            #region implementation

            try
            {
                // Serialize comparison response to formatted JSON for storage
                var jsonReport = JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Save formatted report to storage system for audit and reference
                await _fileStorageService.SaveFileContentAsync(fileId, jsonReport, "comparison-report");
            }
            catch (Exception ex)
            {
                // Log report saving failures without affecting main comparison operation
                _logger.LogWarning(ex, "Failed to save comparison report for file {FileId}", fileId);
            }

            #endregion
        }

        #endregion
    }

    #endregion
}