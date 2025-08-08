namespace MedRecPro.Services
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
    /// comparing original XML structured product labeling files with their JSON representations to identify
    /// missing data, structural discrepancies, and completeness metrics. This is critical for maintaining
    /// data integrity in pharmaceutical and medical device documentation systems.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Dependency injection setup
    /// services.AddScoped&lt;IComparisonService, ComparisonService&gt;();
    /// 
    /// // Usage in controller or service
    /// public async Task&lt;IActionResult&gt; GenerateReport(ComparisonRequest request)
    /// {
    ///     var isReady = await _comparisonService.IsFileReadyForComparisonAsync(request.FileId);
    ///     if (!isReady)
    ///         return BadRequest("File not ready for comparison");
    ///         
    ///     var result = await _comparisonService.GenerateComparisonAsync(
    ///         request.FileId, 
    ///         request.AdditionalInstructions);
    ///     return Ok(result);
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="Models.ComparisonRequest"/>
    /// <seealso cref="Models.ComparisonResponse"/>
    /// <seealso cref="Models.ComparisonResult"/>
    /// <seealso cref="IClaudeApiService"/>
    /// <seealso cref="IFileStorageService"/>
    public interface IComparisonService
    {
        #region comparison analysis methods

        /// <summary>
        /// Asynchronously generates a comprehensive comparison analysis between SPL XML and JSON representations
        /// using AI-powered analysis to assess data completeness, identify discrepancies, and provide detailed metrics.
        /// </summary>
        /// <param name="fileId">
        /// The unique identifier of the uploaded SPL file to be analyzed. This identifier is used to retrieve
        /// both the original XML file and its corresponding JSON representation for comparison.
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
        /// <item>Retrieves the original SPL XML content and corresponding JSON representation</item>
        /// <item>Constructs an AI-optimized prompt incorporating the file contents and any additional instructions</item>
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
        /// var basicResult = await comparisonService.GenerateComparisonAsync("file-123");
        /// 
        /// // Targeted comparison with specific focus areas
        /// var targetedResult = await comparisonService.GenerateComparisonAsync(
        ///     "file-456", 
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
        /// Thrown when the fileId is null, empty, or whitespace.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Thrown when the specified file cannot be found in the storage system.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the file is not ready for comparison or when required file formats are missing.
        /// </exception>
        /// <exception cref="ExternalServiceException">
        /// Thrown when the Claude AI service is unavailable or returns an error response.
        /// </exception>
        /// <seealso cref="Models.ComparisonResponse"/>
        /// <seealso cref="Models.ComparisonResult"/>
        /// <seealso cref="IsFileReadyForComparisonAsync(string)"/>
        /// <seealso cref="IClaudeApiService.GenerateCompletionAsync(string)"/>
        Task<Models.ComparisonResponse> GenerateComparisonAsync(string fileId, string? additionalInstructions = null);

        #endregion

        #region file readiness validation methods

        /*******************************************************************************/
        /// <summary>
        /// Asynchronously determines whether a specified file is ready for comparison analysis
        /// by verifying the availability of both XML and JSON representations in the storage system.
        /// </summary>
        /// <param name="fileId">
        /// The unique identifier of the file to check for readiness. This identifier should correspond
        /// to a previously uploaded SPL file that has undergone initial processing and conversion.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous readiness check operation. The task result is
        /// <c>true</c> if both XML and JSON representations are available and accessible for comparison;
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method performs essential validation before attempting comparison analysis, ensuring that:
        /// <list type="bullet">
        /// <item>The original SPL XML file exists and is accessible in the storage system</item>
        /// <item>A corresponding JSON representation has been generated and is available</item>
        /// <item>Both files are in a valid state for AI comparison processing</item>
        /// <item>File permissions and access controls allow for reading the content</item>
        /// </list>
        /// This pre-check helps prevent failures during the more resource-intensive comparison process
        /// and provides early feedback to client applications about file availability.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Check file readiness before initiating comparison
        /// string fileId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
        /// 
        /// if (await comparisonService.IsFileReadyForComparisonAsync(fileId))
        /// {
        ///     // File is ready, proceed with comparison
        ///     var comparisonResult = await comparisonService.GenerateComparisonAsync(fileId);
        ///     ProcessComparisonResults(comparisonResult);
        /// }
        /// else
        /// {
        ///     // File not ready, inform user or retry later
        ///     Console.WriteLine("File is not ready for comparison. Please try again later.");
        /// }
        /// 
        /// // Usage in API endpoint with proper error handling
        /// [HttpGet("ready/{fileId}")]
        /// public async Task&lt;IActionResult&gt; CheckFileReadiness(string fileId)
        /// {
        ///     try
        ///     {
        ///         var isReady = await _comparisonService.IsFileReadyForComparisonAsync(fileId);
        ///         return Ok(isReady);
        ///     }
        ///     catch (ArgumentException)
        ///     {
        ///         return BadRequest("Invalid file identifier");
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <exception cref="ArgumentException">
        /// Thrown when the fileId parameter is null, empty, or contains only whitespace characters.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown when the application lacks sufficient permissions to access the file storage system
        /// or specific file locations.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown when there are underlying file system issues preventing access to storage locations.
        /// </exception>
        /// <seealso cref="GenerateComparisonAsync(string, string)"/>
        /// <seealso cref="IFileStorageService"/>
        /// <seealso cref="Models.ComparisonRequest"/>
        Task<bool> IsFileReadyForComparisonAsync(string fileId);

        #endregion
    }

    #endregion
}