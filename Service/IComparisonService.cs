using MedRecPro.Models;
using MedRecPro.Service;

namespace MedRecPro.Service
{
    #region comparison service interface

    /*******************************************************************************/
    // Enhanced model classes for MedRecPro AI comparison integration
    /// <summary>
    /// Defines the contract for SPL (Structured Product Labeling) comparison services that analyze
    /// the completeness and accuracy of medical record data transformations between XML and JSON formats.
    /// This interface provides AI-powered comparison capabilities for ensuring data integrity in medical documentation.
    /// </summary>
    /// <remarks>
    /// The comparison service leverages artificial intelligence to perform systematic analysis of SPL documents,
    /// comparing original XML structured product labeling data with their JSON representations to identify
    /// missing data, structural discrepancies, and completeness metrics. This is critical for maintaining
    /// data integrity in pharmaceutical and medical device documentation systems.
    /// 
    /// The service operates on SPL data records stored in the database, accessed via GUID identifiers,
    /// rather than file system operations, providing better performance and data consistency.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Dependency injection setup
    /// services.AddScoped&lt;IComparisonService, ComparisonService&gt;();
    /// 
    /// // Usage in controller or service
    /// public async Task&lt;IActionResult&gt; GenerateReport(ComparisonRequest request)
    /// {
    ///     var isReady = await _comparisonService.IsSplDataReadyForComparisonAsync(request.SplDataGuid);
    ///     if (!isReady)
    ///         return BadRequest("SPL data not ready for comparison");
    ///         
    ///     var result = await _comparisonService.GenerateComparisonAsync(
    ///         request.SplDataGuid, 
    ///         request.AdditionalInstructions);
    ///     return Ok(result);
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="Models.ComparisonRequest"/>
    /// <seealso cref="Models.ComparisonResponse"/>
    /// <seealso cref="Models.ComparisonResult"/>
    /// <seealso cref="IClaudeApiService"/>
    /// <seealso cref="SplDataService"/>
    public interface IComparisonService
    {
        #region comparison analysis methods

        /// <summary>
        /// Asynchronously generates a comprehensive comparison analysis between SPL XML and JSON representations
        /// using AI-powered analysis to assess data completeness, identify discrepancies, and provide detailed metrics.
        /// </summary>
        /// <param name="splDataGuid">
        /// The unique GUID identifier of the SPL data record to be analyzed. This GUID is used to retrieve
        /// the SPL data from the database including the original XML content and any associated metadata.
        /// </param>
        /// <param name="additionalInstructions">
        /// Optional additional instructions to guide the AI comparison analysis. These can specify particular
        /// areas of focus such as clinical trial data, drug interactions, safety information, or other specific
        /// sections of the SPL document that require special attention during the comparison process.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous comparison operation. The task result contains a
        /// <see cref="Models.ComparisonResponse"/> with comprehensive analysis results including completeness
        /// assessment, detailed findings, identified issues, and quantitative metrics about data preservation.
        /// </returns>
        /// <remarks>
        /// This method performs the core comparison functionality of the system, orchestrating the following operations:
        /// <list type="number">
        /// <item>Retrieves the SPL data record from the database using the provided GUID</item>
        /// <item>Extracts the original SPL XML content from the database record</item>
        /// <item>Generates or retrieves the corresponding JSON representation</item>
        /// <item>Constructs an AI-optimized prompt incorporating the content and any additional instructions</item>
        /// <item>Invokes the Claude AI service to perform detailed comparison analysis</item>
        /// <item>Parses the AI response to extract completeness assessment, issues, and metrics</item>
        /// <item>Returns structured results for consumption by client applications</item>
        /// </list>
        /// The comparison focuses on ensuring that all critical medical information from the XML is accurately
        /// preserved in the JSON format, including drug safety data, clinical trial results, dosage information,
        /// contraindications, and regulatory compliance details.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Basic comparison without additional instructions
        /// var splGuid = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");
        /// var basicResult = await comparisonService.GenerateComparisonAsync(splGuid);
        /// 
        /// // Targeted comparison with specific focus areas
        /// var targetedResult = await comparisonService.GenerateComparisonAsync(
        ///     splGuid, 
        ///     "Focus on drug interaction data completeness and clinical trial section accuracy");
        /// 
        /// // Process results
        /// if (targetedResult.Result.IsComplete)
        /// {
        ///     Console.WriteLine($"✅ Complete: {targetedResult.Result.Summary}");
        /// }
        /// else
        /// {
        ///     Console.WriteLine($"❌ Issues found: {targetedResult.Result.Issues.Count} issues");
        ///     foreach (var issue in targetedResult.Result.Issues)
        ///     {
        ///         Console.WriteLine($"- {issue.Severity}: {issue.Description}");
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <exception cref="ArgumentException">
        /// Thrown when the splDataGuid is empty or invalid.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the SPL data cannot be found in the database or when XML content is missing or invalid.
        /// </exception>
        /// <seealso cref="Models.ComparisonResponse"/>
        /// <seealso cref="Models.ComparisonResult"/>
        /// <seealso cref="IsSplDataReadyForComparisonAsync(Guid)"/>
        /// <seealso cref="SplDataService.GetSplDataByGuidAsync(Guid)"/>
        Task<Models.ComparisonResponse> GenerateComparisonAsync(Guid splDataGuid, string? additionalInstructions = null);

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
        /// <example>
        /// <code>
        /// // Generate document comparison analysis
        /// var documentGuid = Guid.Parse("12345678-1234-1234-1234-123456789012");
        /// var result = await comparisonService.GenerateDocumentComparisonAsync(documentGuid);
        /// 
        /// // Process analysis results
        /// Console.WriteLine($"Document {documentGuid}:");
        /// Console.WriteLine($"Complete: {result.IsComplete}");
        /// Console.WriteLine($"Completion: {result.CompletionPercentage}%");
        /// Console.WriteLine($"Issues found: {result.Differences.Count}");
        /// 
        /// // Review specific differences
        /// foreach (var difference in result.Differences.Where(d => d.Severity == "Critical"))
        /// {
        ///     Console.WriteLine($"CRITICAL: {difference.Description} in {difference.Section}");
        /// }
        /// </code>
        /// </example>
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
        Task<DocumentComparisonResult> GenerateDocumentComparisonAsync(Guid documentGuid);

        #endregion

        #region SPL data readiness validation methods

        /*******************************************************************************/
        /// <summary>
        /// Asynchronously determines whether a specified SPL data record is ready for comparison analysis
        /// by verifying the availability of required XML content in the database.
        /// </summary>
        /// <param name="splDataGuid">
        /// The unique GUID identifier of the SPL data record to check for readiness. This GUID should correspond
        /// to a previously created SPL data record that contains the required XML content for comparison.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous readiness check operation. The task result is
        /// <c>true</c> if the SPL data record exists and contains valid XML content for comparison;
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method performs essential validation before attempting comparison analysis, ensuring that:
        /// <list type="bullet">
        /// <item>The SPL data record exists in the database with the specified GUID</item>
        /// <item>The SPL data record contains valid XML content in the SplXML field</item>
        /// <item>The XML content is not null or empty and is accessible for processing</item>
        /// <item>The SPL data record is not archived or in an invalid state</item>
        /// </list>
        /// This pre-check helps prevent failures during the more resource-intensive comparison process
        /// and provides early feedback to client applications about data availability. JSON representation
        /// availability is validated during the comparison process itself, as it can be generated on-demand.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Check SPL data readiness before initiating comparison
        /// var splGuid = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");
        /// 
        /// if (await comparisonService.IsSplDataReadyForComparisonAsync(splGuid))
        /// {
        ///     // SPL data is ready, proceed with comparison
        ///     var comparisonResult = await comparisonService.GenerateComparisonAsync(splGuid);
        ///     ProcessComparisonResults(comparisonResult);
        /// }
        /// else
        /// {
        ///     // SPL data not ready, inform user or handle appropriately
        ///     Console.WriteLine("SPL data is not ready for comparison. Please ensure data exists and contains XML content.");
        /// }
        /// 
        /// // Usage in API endpoint with proper error handling
        /// [HttpGet("ready/{splDataGuid}")]
        /// public async Task&lt;IActionResult&gt; CheckSplDataReadiness(Guid splDataGuid)
        /// {
        ///     try
        ///     {
        ///         var isReady = await _comparisonService.IsSplDataReadyForComparisonAsync(splDataGuid);
        ///         return Ok(new { IsReady = isReady, SplDataGuid = splDataGuid });
        ///     }
        ///     catch (ArgumentException)
        ///     {
        ///         return BadRequest("Invalid SPL data GUID");
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <exception cref="ArgumentException">
        /// Thrown when the splDataGuid parameter is empty or invalid (Guid.Empty).
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown when the application lacks sufficient permissions to access the database
        /// or specific SPL data records.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when there are underlying database connectivity issues preventing access to SPL data.
        /// </exception>
        /// <seealso cref="GenerateComparisonAsync(Guid, string)"/>
        /// <seealso cref="SplDataService"/>
        /// <seealso cref="Models.ComparisonRequest"/>
        /// <seealso cref="Models.SplData"/>
        Task<bool> IsSplDataReadyForComparisonAsync(Guid splDataGuid);

        #endregion
    }

    #endregion
}