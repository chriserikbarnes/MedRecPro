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
        /// observations are split into multiple requests. Lower values
        /// produce shorter responses that are less likely to be truncated.
        /// </summary>
        public int MaxObservationsPerRequest { get; set; } = 500;

        /**************************************************************/
        /// <summary>
        /// Delay in milliseconds between API requests to respect rate limits. Default 200ms.
        /// </summary>
        public int DelayBetweenRequestsMs { get; set; } = 200;

        /**************************************************************/
        /// <summary>
        /// Parse-quality threshold for gating observations to Claude. Observations whose
        /// <c>QC_PARSE_QUALITY:{score}</c> value is strictly LESS THAN this threshold are
        /// forwarded to Claude for semantic review; observations at or above the threshold
        /// skip the API correction pass. Set to 0.0 to send every observation (rarely useful —
        /// defeats the cost gate). Default 0.75 means "send anything with at least one hard
        /// parse failure, two soft repairs, or low parser confidence".
        /// </summary>
        /// <remarks>
        /// Replaces the retired <c>MlAnomalyScoreThreshold</c>. The parse-quality signal is
        /// deterministic and targets parse-alignment failures directly (null PrimaryValue,
        /// Text-typed rows, null ParameterName in categories where it is Required, garbage
        /// Unit/ParameterSubtype content, etc.) rather than using PCA reconstruction error
        /// as a noisy proxy for parse failure. Evaluated by
        /// <see cref="MedRecProImportClass.Service.TransformationServices.ClaudeApiCorrectionService"/>
        /// at the start of <c>CorrectBatchAsync</c>. Observations without a parse-quality flag
        /// (e.g., because the quality service was not registered) always pass through
        /// (conservative — send to Claude when quality is unknown).
        /// </remarks>
        /// <seealso cref="QCNetCorrectionSettings"/>
        /// <seealso cref="MedRecProImportClass.Service.TransformationServices.IParseQualityService"/>
        public float ClaudeReviewQualityThreshold { get; set; } = 0.75f;

        /**************************************************************/
        /// <summary>
        /// Fields the correction service refuses to mutate even when Claude proposes
        /// a correction. This setting narrows the hardcoded service allowlist; it never
        /// expands the set of correctable fields.
        /// </summary>
        public HashSet<string> ProtectedFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Rejects TreatmentArm rewrites that change placebo-classification semantics.
        /// </summary>
        /// <remarks>
        /// The guard mirrors Stage 5 comparator semantics: placebo-equivalent arms match
        /// placebo, sham, or vehicle text, or have Dose equal to 0.
        /// </remarks>
        public bool RejectPlaceboClassFlip { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Rejects ParameterName rewrites where the proposed name is a strict token
        /// superset of the original name.
        /// </summary>
        public bool RejectParameterNameSuperset { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Rejects Unit="%" assignments when the row remains text-typed after all
        /// accepted corrections.
        /// </summary>
        public bool RejectTextRowUnitPercent { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Rejects non-empty TreatmentArm values being cleared unless the original arm
        /// is a known header or generic-label echo.
        /// </summary>
        public bool RejectTreatmentArmToNullUnlessHeaderEcho { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Rejects TreatmentArm proposals that are MedDRA SOC or body-system labels.
        /// </summary>
        public bool RejectTreatmentArmBodySystem { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Rejects TreatmentArm proposals that exactly match a source table header token.
        /// </summary>
        public bool RejectTreatmentArmHeaderToken { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Enforces Percentage plus percent-unit consistency for numeric cells under
        /// source table headers that contain a percent sign.
        /// </summary>
        public bool EnforcePercentColumnConsistency { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Short TreatmentArm abbreviations that must be preserved as real arms rather
        /// than treated as header echoes or all-caps study labels.
        /// </summary>
        public HashSet<string> ProtectedShortTreatmentArms { get; set; } =
            new(StringComparer.OrdinalIgnoreCase) { "BSC", "SoC", "BAT" };
        /**************************************************************/
        /// <summary>
        /// Path to the skill file containing the correction system prompt. Relative paths
        /// are resolved from the application base directory. When the file exists, its content
        /// (after YAML frontmatter stripping) replaces the hardcoded system prompt.
        /// </summary>
        /// <remarks>
        /// Skill file format: YAML frontmatter delimited by <c>---</c> lines, followed by
        /// the prompt text. The frontmatter is stripped; only the body is used as the system prompt.
        /// Falls back to the embedded default prompt if the file is missing or empty.
        /// </remarks>
        public string SkillFilePath { get; set; } = "Skills/correction-system-prompt.md";

        /**************************************************************/
        /// <summary>
        /// Path to the skill file containing pivot table comparison instructions. Appended to
        /// the user message when an original <see cref="ReconstructedTable"/> is available.
        /// </summary>
        public string PivotComparisonSkillPath { get; set; } = "Skills/pivot-comparison-prompt.md";

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
