namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Configuration settings for the Claude API post-parse correction service (Stage 3.5).
    /// Controls model selection, rate limiting, and enablement of AI-powered observation
    /// correction in the SPL Table Normalization pipeline.
    /// </summary>
    /// <remarks>
    /// The correction service runs after Stage 3 parsers produce <see cref="ParsedObservation"/>
    /// objects but before they are written to <c>tmp_FlattenedStandardizedTable</c>. It sends
    /// batches of observations to Claude for semantic review and correction of misclassified
    /// fields (e.g., wrong PrimaryValueType, swapped TreatmentArm/ParameterName).
    ///
    /// The API key should be stored in User Secrets for the consuming console project:
    /// <code>
    /// dotnet user-secrets set "ClaudeApiCorrectionSettings:ApiKey" "sk-ant-..."
    /// </code>
    /// </remarks>
    /// <seealso cref="MedRecProImportClass.Service.TransformationServices.IClaudeApiCorrectionService"/>
    public class ClaudeApiCorrectionSettings
    {
        #region authentication properties

        /**************************************************************/
        /// <summary>
        /// Anthropic API key. Store in User Secrets — never commit to source control.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        #endregion

        #region model configuration properties

        /**************************************************************/
        /// <summary>
        /// Claude model identifier. Defaults to Haiku for fast, low-cost batch processing.
        /// </summary>
        public string Model { get; set; } = "claude-haiku-4-5-20251001";

        /**************************************************************/
        /// <summary>
        /// Maximum tokens for the correction response. With max 10 corrections per batch
        /// (enforced by system prompt), 4000 tokens provides adequate headroom.
        /// </summary>
        public int MaxTokens { get; set; } = 4000;

        /**************************************************************/
        /// <summary>
        /// Temperature for correction responses. 0.0 for maximum determinism — corrections
        /// should be consistent and reproducible.
        /// </summary>
        public double Temperature { get; set; } = 0.0;

        #endregion

        #region processing configuration properties

        /**************************************************************/
        /// <summary>
        /// Master enable/disable switch. When false, the correction service is a no-op
        /// and all observations pass through unmodified.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Maximum observations to include in a single API request. Tables with more
        /// observations are split into multiple requests. Default 20. Lower values
        /// produce shorter responses that are less likely to be truncated.
        /// </summary>
        public int MaxObservationsPerRequest { get; set; } = 20;

        /**************************************************************/
        /// <summary>
        /// Delay in milliseconds between API requests to respect rate limits. Default 200ms.
        /// </summary>
        public int DelayBetweenRequestsMs { get; set; } = 200;

        #endregion

        #region api connection properties

        /**************************************************************/
        /// <summary>
        /// Anthropic Messages API base URL.
        /// </summary>
        public string BaseUrl { get; set; } = "https://api.anthropic.com/v1/messages";

        /**************************************************************/
        /// <summary>
        /// Anthropic API version header value.
        /// </summary>
        public string AnthropicVersion { get; set; } = "2023-06-01";

        #endregion
    }
}
