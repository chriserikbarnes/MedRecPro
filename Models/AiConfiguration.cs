using MedRecPro.Service;

namespace MedRecPro.Models
{
    #region claude api configuration settings

    /**************************************************************/
    /// <summary>
    /// Configuration settings for Anthropic Claude AI API integration, providing authentication,
    /// model selection, and processing parameters for AI-powered medical document analysis.
    /// This class defines the connection and operational parameters required for secure and
    /// efficient integration with Claude's language model capabilities in healthcare applications.
    /// </summary>
    /// <remarks>
    /// The ClaudeApiSettings class encapsulates all configuration parameters necessary for
    /// establishing and maintaining secure communication with Anthropic's Claude AI service.
    /// These settings are critical for ensuring proper authentication, optimal model performance,
    /// and appropriate resource allocation for medical document processing workflows.
    /// 
    /// Configuration considerations for medical document analysis include:
    /// - API key security and rotation policies for healthcare data protection
    /// - Model selection based on analysis complexity and accuracy requirements
    /// - Token limits optimized for SPL document size and analysis depth
    /// - Rate limiting and retry policies for enterprise healthcare applications
    /// 
    /// This configuration supports HIPAA-compliant medical document processing by ensuring
    /// secure API communication and appropriate data handling practices for sensitive
    /// pharmaceutical and clinical information.
    /// </remarks>
    /// <example>
    /// <code>
    /// // appsettings.json configuration
    /// {
    ///   "ClaudeApi": {
    ///     "ApiKey": "sk-ant-your-secure-api-key-here",
    ///     "Model": "claude-3-sonnet-20240229",
    ///     "MaxTokens": 4000,
    ///     "EnableThinking": true,
    ///     "Temperature": 0.1
    ///   }
    /// }
    /// 
    /// // Dependency injection setup
    /// services.Configure&lt;ClaudeApiSettings&gt;(configuration.GetSection("ClaudeApi"));
    /// 
    /// // Usage in service constructor
    /// public ClaudeApiService(IOptions&lt;ClaudeApiSettings&gt; settings)
    /// {
    ///     _settings = settings.Value;
    ///     _httpClient.DefaultRequestHeaders.Add("x-api-key", _settings.ApiKey);
    /// }
    /// 
    /// // Environment-specific configuration for production
    /// services.Configure&lt;ClaudeApiSettings&gt;(options =&gt;
    /// {
    ///     options.ApiKey = Environment.GetEnvironmentVariable("CLAUDE_API_KEY") ?? 
    ///                      throw new InvalidOperationException("Claude API key not found");
    ///     options.MaxTokens = Environment.GetEnvironmentVariable("ENVIRONMENT") == "Production" ? 8000 : 4000;
    ///     options.Temperature = Environment.GetEnvironmentVariable("ENVIRONMENT") == "Production" ? 0.0 : 0.1;
    /// });
    /// </code>
    /// </example>
    /// <seealso cref="IClaudeApiService"/>
    /// <seealso cref="ComparisonSettings"/>
    /// <seealso cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>
    public class ClaudeApiSettings
    {
        #region authentication properties

        /**************************************************************/
        /// <summary>
        /// Gets or sets the secure API authentication key for accessing Anthropic's Claude AI service.
        /// This key provides authorized access to AI capabilities and must be protected according
        /// to healthcare data security standards and organizational security policies.
        /// </summary>
        /// <remarks>
        /// The API key serves as the primary authentication mechanism for Claude AI service access
        /// and should be:
        /// - Stored securely using configuration providers or secret management systems
        /// - Never committed to source control or logged in application outputs
        /// - Rotated regularly according to security best practices
        /// - Protected with appropriate access controls in production environments
        /// 
        /// For healthcare applications processing PHI (Protected Health Information), additional
        /// security measures should be implemented including encryption at rest and in transit,
        /// audit logging of API key usage, and compliance with HIPAA security requirements.
        /// </remarks>
        /// <example>
        /// Production configuration should use environment variables:
        /// <code>
        /// // Environment variable setup
        /// export CLAUDE_API_KEY="sk-ant-your-secure-production-key"
        /// 
        /// // Configuration binding
        /// options.ApiKey = Environment.GetEnvironmentVariable("CLAUDE_API_KEY");
        /// </code>
        /// </example>
        /// <seealso cref="System.Environment.GetEnvironmentVariable(string)"/>
        /// <seealso cref="Microsoft.Extensions.Configuration.IConfiguration"/>
        public string ApiKey { get; set; } = string.Empty;

        #endregion

        #region model configuration properties

        /**************************************************************/
        /// <summary>
        /// Gets or sets the Claude AI model identifier to use for medical document analysis operations.
        /// This determines the specific version and capabilities of the AI model employed for
        /// SPL comparison, medical terminology processing, and healthcare document validation.
        /// </summary>
        /// <remarks>
        /// Model selection impacts analysis quality, processing speed, and cost considerations
        /// for medical document workflows. Available models provide different capabilities:
        /// 
        /// - claude-3-sonnet-20240229: Balanced performance for general medical document analysis
        /// - claude-3-opus-20240229: Enhanced accuracy for complex pharmaceutical documentation
        /// - claude-3-haiku-20240307: Optimized speed for high-volume document processing
        /// 
        /// For SPL (Structured Product Labeling) analysis, Sonnet provides optimal balance
        /// of accuracy and performance for regulatory compliance validation and data completeness
        /// assessment. Model selection should consider document complexity, processing volume,
        /// and accuracy requirements for specific medical use cases.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Standard configuration for medical document analysis
        /// settings.Model = "claude-3-sonnet-20240229";
        /// 
        /// // High-accuracy configuration for complex pharmaceutical documents
        /// settings.Model = "claude-3-opus-20240229";
        /// 
        /// // High-volume processing configuration
        /// settings.Model = "claude-3-haiku-20240307";
        /// 
        /// // Environment-based model selection
        /// settings.Model = environment == "Production" ? 
        ///     "claude-3-opus-20240229" : "claude-3-sonnet-20240229";
        /// </code>
        /// </example>
        /// <seealso cref="MaxTokens"/>
        /// <seealso cref="Temperature"/>
        /// <seealso cref="EnableThinking"/>
        public string Model { get; set; } = "claude-sonnet-4-20250514";

        /**************************************************************/
        /// <summary>
        /// Gets or sets a value indicating whether to enable Claude AI's thinking mode
        /// for enhanced reasoning and analysis during medical document processing operations.
        /// When enabled, the AI model provides additional reasoning steps and detailed
        /// analytical processes that improve accuracy for complex pharmaceutical documentation.
        /// </summary>
        /// <remarks>
        /// Thinking mode enables Claude to perform more deliberate, step-by-step reasoning
        /// when analyzing medical documents, resulting in higher accuracy and more thorough
        /// analysis for critical healthcare applications. Benefits include:
        /// 
        /// - Enhanced accuracy for complex SPL document validation and comparison
        /// - Improved detection of subtle inconsistencies in pharmaceutical labeling
        /// - More comprehensive analysis of regulatory compliance requirements
        /// - Better handling of medical terminology disambiguation and context
        /// - Detailed reasoning trails for audit and quality assurance purposes
        /// 
        /// For medical document processing, thinking mode is particularly valuable for:
        /// - Critical pharmaceutical safety documentation requiring high accuracy
        /// - Regulatory compliance validation with detailed justification requirements
        /// - Complex clinical trial data analysis and interpretation
        /// - Medical device labeling validation with safety-critical considerations
        /// 
        /// Performance considerations:
        /// - Increased processing time and token usage for enhanced analysis
        /// - Higher API costs due to extended reasoning processes
        /// - More detailed responses requiring additional storage and processing capacity
        /// 
        /// Thinking mode should be enabled for production healthcare applications where
        /// accuracy is paramount and disabled for high-volume processing scenarios where
        /// speed is prioritized over detailed analytical reasoning.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Enable thinking mode for critical medical document analysis
        /// settings.EnableThinking = true;
        /// 
        /// // Disable for high-volume batch processing
        /// settings.EnableThinking = false;
        /// 
        /// // Conditional thinking based on document criticality
        /// settings.EnableThinking = documentType == "PharmaceuticalSafety" || 
        ///                          documentType == "ClinicalTrial";
        /// 
        /// // Environment-based configuration
        /// settings.EnableThinking = Environment.GetEnvironmentVariable("ENVIRONMENT") == "Production" &amp;&amp;
        ///                          Environment.GetEnvironmentVariable("ENABLE_DETAILED_ANALYSIS") == "true";
        /// 
        /// // Dynamic thinking mode based on document complexity
        /// settings.EnableThinking = documentSize > 50000 || containsComplexMedicalTerminology;
        /// </code>
        /// </example>
        /// <seealso cref="Temperature"/>
        /// <seealso cref="MaxTokens"/>
        /// <seealso cref="Model"/>
        public bool EnableThinking { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the temperature value controlling the randomness and creativity
        /// of Claude AI responses during medical document analysis operations. Lower values
        /// produce more deterministic and consistent results, while higher values introduce
        /// controlled variability for comprehensive analysis perspectives in healthcare applications.
        /// </summary>
        /// <remarks>
        /// Temperature significantly impacts the consistency and variability of AI analysis
        /// for medical documents, with critical implications for healthcare data processing:
        /// 
        /// Temperature ranges and medical application suitability:
        /// - 0.0: Maximum determinism for regulatory compliance and safety-critical analysis
        /// - 0.1-0.3: Controlled consistency for standard SPL comparison and validation
        /// - 0.4-0.7: Moderate creativity for comprehensive analysis and alternative perspectives
        /// - 0.8-1.0: High variability for exploratory analysis and research applications
        /// 
        /// For healthcare applications requiring regulatory compliance and audit trails,
        /// lower temperature values (0.0-0.2) ensure consistent, reproducible results
        /// that meet FDA and other regulatory body requirements for pharmaceutical documentation.
        /// 
        /// AUTHOR NOTE 08/11/2025: I'm not aware of any specific regulation that surrounds the temperature setting.
        /// I don't even think there is a guidance.
        /// 
        /// Medical document processing considerations:
        /// - Pharmaceutical safety documentation requires deterministic analysis (temperature 0.0)
        /// - SPL comparison operations benefit from slight variability (temperature 0.1-0.2)
        /// - Clinical research analysis may use moderate creativity (temperature 0.3-0.5)
        /// - Medical literature review can accommodate higher variability (temperature 0.5-0.7)
        /// 
        /// Consistency requirements for healthcare applications:
        /// - Regulatory submissions must produce identical results for identical inputs
        /// - Quality assurance workflows require reproducible validation outcomes
        /// - Audit compliance necessitates consistent analysis methodologies
        /// - Patient safety documentation demands deterministic processing
        /// 
        /// Temperature selection should align with the criticality of medical decisions
        /// based on the AI analysis results and regulatory compliance requirements.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Maximum determinism for regulatory compliance
        /// settings.Temperature = 0.0;
        /// 
        /// // Standard SPL comparison with slight variability
        /// settings.Temperature = 0.1;
        /// 
        /// // Comprehensive analysis with controlled creativity
        /// settings.Temperature = 0.3;
        /// 
        /// // Research applications with moderate variability
        /// settings.Temperature = 0.5;
        /// 
        /// // Environment-based temperature configuration
        /// settings.Temperature = Environment.GetEnvironmentVariable("ENVIRONMENT") == "Production" ? 0.0 : 0.2;
        /// 
        /// // Document-type specific temperature settings
        /// settings.Temperature = documentType switch
        /// {
        ///     "PharmaceuticalSafety" => 0.0,
        ///     "SPLComparison" => 0.1,
        ///     "ClinicalResearch" => 0.3,
        ///     "MedicalLiterature" => 0.5,
        ///     _ => 0.1
        /// };
        /// 
        /// // Validation for medical document processing
        /// if (settings.Temperature > 0.3 &amp;&amp; documentContainsSafetyData)
        /// {
        ///     throw new ArgumentException("Safety-critical documents require temperature ≤ 0.3");
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="EnableThinking"/>
        /// <seealso cref="Model"/>
        /// <seealso cref="MaxTokens"/>
        public double Temperature { get; set; } = 0.1;

        #endregion

        #region processing limit properties

        /**************************************************************/
        /// <summary>
        /// Gets or sets the maximum number of tokens allowed for AI completion responses
        /// during medical document analysis operations. This limit controls response length,
        /// processing costs, and ensures appropriate resource allocation for SPL comparison tasks.
        /// </summary>
        /// <remarks>
        /// Token limits directly impact the depth and comprehensiveness of AI analysis responses
        /// for medical documents. Considerations for medical document processing include:
        /// 
        /// - 4000 tokens: Suitable for standard SPL section analysis and basic comparison reports
        /// - 8000 tokens: Appropriate for comprehensive multi-section analysis and detailed findings
        /// - 16000 tokens: Required for complex pharmaceutical documents with extensive data
        /// 
        /// For SPL comparison operations, 4000 tokens typically provides sufficient capacity
        /// for detailed analysis including completeness assessment, issue identification,
        /// and quantitative metrics. Larger limits may be necessary for complex pharmaceutical
        /// documents with extensive clinical trial data or regulatory compliance requirements.
        /// 
        /// Token usage impacts API costs and response times, requiring balance between
        /// analysis comprehensiveness and operational efficiency for healthcare applications.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Standard SPL comparison analysis
        /// settings.MaxTokens = 4000;
        /// 
        /// // Comprehensive pharmaceutical document analysis
        /// settings.MaxTokens = 8000;
        /// 
        /// // Complex regulatory compliance validation
        /// settings.MaxTokens = 16000;
        /// 
        /// // Dynamic token allocation based on document complexity
        /// settings.MaxTokens = documentSize &gt; 100000 ? 8000 : 4000;
        /// 
        /// // Environment-based configuration
        /// settings.MaxTokens = Environment.GetEnvironmentVariable("ENVIRONMENT") == "Production" ? 8000 : 4000;
        /// </code>
        /// </example>
        /// <seealso cref="Model"/>
        /// <seealso cref="Temperature"/>
        /// <seealso cref="EnableThinking"/>
        /// <seealso cref="ComparisonSettings.MaxPromptLength"/>
        public int MaxTokens { get; set; } = 4000;

        #endregion
    }

    #endregion

    #region comparison service configuration settings

    /**************************************************************/
    /// <summary>
    /// Configuration settings for SPL comparison service operations, controlling report generation,
    /// prompt processing limits, and performance optimization features for medical document
    /// validation workflows. This class defines operational parameters that ensure efficient
    /// and reliable comparison analysis for pharmaceutical and healthcare documentation systems.
    /// </summary>
    /// <remarks>
    /// The ComparisonSettings class provides fine-grained control over comparison service behavior,
    /// enabling optimization for different deployment scenarios and operational requirements.
    /// These settings are essential for maintaining system performance, managing storage resources,
    /// and ensuring appropriate processing limits for medical document analysis workflows.
    /// 
    /// Configuration considerations for medical document comparison include:
    /// - Report persistence strategies for regulatory compliance and audit requirements
    /// - Prompt size limits optimized for SPL document complexity and AI processing capabilities
    /// - Caching policies to improve performance for repeated analysis operations
    /// - Resource management for high-volume pharmaceutical document processing
    /// 
    /// For healthcare applications, these settings support HIPAA compliance requirements
    /// through controlled data retention, secure caching mechanisms, and appropriate
    /// processing limits for sensitive medical information.
    /// </remarks>
    /// <example>
    /// <code>
    /// // appsettings.json configuration
    /// {
    ///   "Comparison": {
    ///     "SaveReports": true,
    ///     "MaxPromptLength": 100000,
    ///     "EnableCaching": true
    ///   }
    /// }
    /// 
    /// // Dependency injection setup
    /// services.Configure&lt;ComparisonSettings&gt;(configuration.GetSection("Comparison"));
    /// 
    /// // Usage in comparison service
    /// public ComparisonService(IOptions&lt;ComparisonSettings&gt; settings)
    /// {
    ///     _settings = settings.Value;
    ///     _cacheEnabled = _settings.EnableCaching;
    /// }
    /// 
    /// // Environment-specific configuration
    /// services.Configure&lt;ComparisonSettings&gt;(options =&gt;
    /// {
    ///     options.SaveReports = Environment.GetEnvironmentVariable("ENVIRONMENT") == "Production";
    ///     options.EnableCaching = Environment.GetEnvironmentVariable("ENABLE_CACHE") == "true";
    ///     options.MaxPromptLength = int.Parse(Environment.GetEnvironmentVariable("MAX_PROMPT_LENGTH") ?? "100000");
    /// });
    /// </code>
    /// </example>
    /// <seealso cref="IComparisonService"/>
    /// <seealso cref="ClaudeApiSettings"/>
    /// <seealso cref="Models.ComparisonResponse"/>
    public class ComparisonSettings
    {
        #region report management properties

        /**************************************************************/
        /// <summary>
        /// Gets or sets a value indicating whether comparison reports should be automatically
        /// saved to the storage system for audit trail, regulatory compliance, and historical
        /// analysis purposes in medical document validation workflows.
        /// </summary>
        /// <remarks>
        /// Report saving provides essential audit trail capabilities for medical document
        /// processing operations, supporting:
        /// 
        /// - Regulatory compliance requirements for pharmaceutical documentation
        /// - Historical trend analysis for data quality improvement initiatives
        /// - Troubleshooting and error analysis for comparison operation failures
        /// - Quality assurance validation for medical document transformation processes
        /// 
        /// For healthcare applications processing PHI, saved reports should be:
        /// - Encrypted at rest according to HIPAA security requirements
        /// - Subject to appropriate retention policies and data lifecycle management
        /// - Protected with access controls limiting access to authorized personnel
        /// - Included in regular backup and disaster recovery procedures
        /// 
        /// Disabling report saving may be appropriate for development environments,
        /// high-volume processing scenarios with storage constraints, or when external
        /// audit logging systems provide equivalent functionality.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Enable report saving for production compliance
        /// settings.SaveReports = true;
        /// 
        /// // Disable for development or testing environments
        /// settings.SaveReports = false;
        /// 
        /// // Conditional saving based on document type
        /// settings.SaveReports = documentType == "SPL" || documentType == "Clinical";
        /// 
        /// // Environment-based configuration
        /// settings.SaveReports = Environment.GetEnvironmentVariable("ENVIRONMENT") != "Development";
        /// </code>
        /// </example>
        /// <seealso cref="Models.ComparisonResponse"/>
        public bool SaveReports { get; set; } = true;

        #endregion

        #region processing limit properties

        /**************************************************************/
        /// <summary>
        /// Gets or sets the maximum allowed length for AI comparison prompts in characters,
        /// controlling the size and complexity of content sent to Claude AI for medical
        /// document analysis operations. This limit ensures optimal AI processing performance
        /// and prevents resource exhaustion during large document comparison workflows.
        /// </summary>
        /// <remarks>
        /// Prompt length limits are critical for maintaining system performance and managing
        /// AI service costs for medical document processing. Considerations include:
        /// 
        /// - 50,000 characters: Suitable for standard SPL sections and basic document pairs
        /// - 100,000 characters: Appropriate for comprehensive SPL documents with multiple sections
        /// - 200,000 characters: Required for complex pharmaceutical documents with extensive data
        /// 
        /// For SPL comparison operations, prompt content includes:
        /// - Original XML content with complete medical and regulatory information
        /// - Corresponding JSON representation for data completeness validation
        /// - Analysis instructions and specific focus area requirements
        /// - Medical terminology context and regulatory compliance guidelines
        /// 
        /// Exceeding prompt limits may result in truncated analysis or API errors,
        /// requiring document segmentation or selective content analysis for large
        /// pharmaceutical documents. Proper prompt optimization ensures comprehensive
        /// analysis while maintaining processing efficiency.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Standard SPL document processing
        /// settings.MaxPromptLength = 100000;
        /// 
        /// // Large pharmaceutical document support
        /// settings.MaxPromptLength = 200000;
        /// 
        /// // Memory-constrained environment
        /// settings.MaxPromptLength = 50000;
        /// 
        /// // Dynamic limit based on available resources
        /// settings.MaxPromptLength = Environment.ProcessorCount * 25000;
        /// 
        /// // Prompt length validation in service
        /// if (promptContent.Length &gt; _settings.MaxPromptLength)
        /// {
        ///     throw new ArgumentException($"Prompt exceeds maximum length of {_settings.MaxPromptLength}");
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="ClaudeApiSettings.MaxTokens"/>
        public int MaxPromptLength { get; set; } = 100000;

        #endregion

        #region performance optimization properties

        /**************************************************************/
        /// <summary>
        /// Gets or sets a value indicating whether comparison results should be cached
        /// to improve performance for repeated analysis operations on identical medical
        /// documents. Caching reduces AI service calls and provides faster response times
        /// for frequently accessed SPL documents in pharmaceutical validation workflows.
        /// </summary>
        /// <remarks>
        /// Intelligent caching provides significant performance benefits for medical document
        /// processing operations by:
        /// 
        /// - Reducing API calls and associated costs for repeated document analysis
        /// - Improving response times for frequently accessed SPL documents
        /// - Minimizing resource usage during high-volume processing periods
        /// - Providing consistent results for identical document comparison requests
        /// 
        /// For healthcare applications, caching implementation should consider:
        /// - HIPAA compliance requirements for temporary data storage and encryption
        /// - Cache invalidation policies for updated or modified medical documents
        /// - Memory management and cache size limits for system resource optimization
        /// - Cache key generation strategies that maintain data privacy and security
        /// 
        /// Caching is particularly beneficial for:
        /// - Development and testing environments with repeated document processing
        /// - Quality assurance workflows requiring multiple validation passes
        /// - Training and demonstration scenarios using standard SPL documents
        /// - Batch processing operations with potential document duplication
        /// 
        /// Cache disabling may be necessary for real-time production environments
        /// where data freshness is critical or when regulatory requirements prohibit
        /// temporary data retention beyond the immediate processing context.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Enable caching for improved performance
        /// settings.EnableCaching = true;
        /// 
        /// // Disable for real-time production processing
        /// settings.EnableCaching = false;
        /// 
        /// // Environment-based caching configuration
        /// settings.EnableCaching = Environment.GetEnvironmentVariable("ENVIRONMENT") != "Production";
        /// 
        /// // Conditional caching based on document sensitivity
        /// settings.EnableCaching = !documentContainsPHI;
        /// 
        /// // Cache configuration in service implementation
        /// if (_settings.EnableCaching)
        /// {
        ///     var cacheKey = GenerateDocumentHash(xmlContent, jsonContent);
        ///     var cachedResult = await _cache.GetAsync(cacheKey);
        ///     if (cachedResult != null) return cachedResult;
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/>
        /// <seealso cref="Models.ComparisonResponse"/>
        public bool EnableCaching { get; set; } = true;

        #endregion
    }

    #endregion
}