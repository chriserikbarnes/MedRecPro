using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MedRecPro.Service
{
    #region skill service interface

    /**************************************************************/
    /// <summary>
    /// Defines the contract for managing AI skill documents used by the Claude API service.
    /// This interface enables a two-stage routing pattern where skills are first selected
    /// via a lightweight manifest, then fully loaded only when needed to optimize token usage.
    /// </summary>
    /// <remarks>
    /// The skill service implements a scalable architecture for AI prompts:
    ///
    /// <list type="number">
    /// <item><b>Stage 1 - Skill Selection</b>: Uses a lightweight manifest (~500 tokens) containing
    /// skill names and brief descriptions. Claude selects which skill(s) are needed.</item>
    /// <item><b>Stage 2 - Skill Loading</b>: Only the selected skill file(s) are loaded into
    /// the execution prompt, avoiding the overhead of loading all skills (10,000+ tokens).</item>
    /// </list>
    ///
    /// This pattern significantly reduces API costs and improves response latency by minimizing
    /// the prompt size for simple queries that don't require specialized skills.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Two-stage routing example
    /// var manifest = await skillService.GetSkillManifestAsync();
    /// var selectedSkills = await skillService.SelectSkillsAsync(userMessage, manifest);
    /// var fullSkillContent = await skillService.GetSkillContentAsync(selectedSkills);
    /// </code>
    /// </example>
    /// <seealso cref="IClaudeApiService"/>
    /// <seealso cref="GetSkillManifestAsync"/>
    /// <seealso cref="SkillSelection"/>
    public interface IClaudeSkillService
    {
        #region manifest methods

        /**************************************************************/
        /// <summary>
        /// Retrieves the lightweight skill manifest containing names and brief descriptions
        /// of all available skills. This manifest is used in Stage 1 of the two-stage routing
        /// pattern to determine which skill(s) are needed for a given query.
        /// </summary>
        /// <returns>
        /// A task that resolves to the skill manifest content as a string, formatted for
        /// inclusion in the Claude API prompt.
        /// </returns>
        /// <remarks>
        /// The manifest is significantly smaller than the full skill documents (~500 tokens
        /// vs 10,000+ tokens), enabling efficient skill selection with minimal API cost.
        /// </remarks>
        /// <seealso cref="SelectSkillsAsync"/>
        Task<string> GetSkillManifestAsync();

        /**************************************************************/
        /// <summary>
        /// Uses Claude to select the appropriate skill(s) based on a user message and the
        /// skill manifest. This is Stage 1 of the two-stage routing pattern.
        /// </summary>
        /// <param name="userMessage">The user's natural language query.</param>
        /// <param name="systemContext">Optional system context for authentication state, etc.</param>
        /// <returns>
        /// A task that resolves to a <see cref="SkillSelection"/> indicating which skill(s)
        /// should be loaded for processing the query.
        /// </returns>
        /// <remarks>
        /// The selection process uses a fast, lightweight Claude API call with only the
        /// manifest content. This minimizes token usage while still leveraging AI for
        /// accurate skill routing.
        /// </remarks>
        /// <seealso cref="GetSkillManifestAsync"/>
        /// <seealso cref="GetSkillContentAsync"/>
        Task<SkillSelection> SelectSkillsAsync(string userMessage, object? systemContext = null);

        #endregion

        #region skill loading methods

        /**************************************************************/
        /// <summary>
        /// Retrieves the full content of the specified skill(s). This is Stage 2 of the
        /// two-stage routing pattern, loading only the skills that were selected.
        /// </summary>
        /// <param name="selection">The skill selection from Stage 1.</param>
        /// <returns>
        /// A task that resolves to the combined skill content as a string, ready for
        /// inclusion in the Claude API execution prompt.
        /// </returns>
        /// <remarks>
        /// By loading only the selected skills, this method avoids the overhead of including
        /// all skill documentation in every request. This significantly reduces token usage
        /// for queries that only need one or two skills.
        /// </remarks>
        /// <seealso cref="SelectSkillsAsync"/>
        Task<string> GetSkillContentAsync(SkillSelection selection);

        /**************************************************************/
        /// <summary>
        /// Retrieves a specific skill document by name.
        /// </summary>
        /// <param name="skillName">The skill name (e.g., "label", "settings", "userActivity").</param>
        /// <returns>
        /// A task that resolves to the skill content as a string, or an error message
        /// if the skill is not found.
        /// </returns>
        /// <seealso cref="GetAvailableSkillsAsync"/>
        Task<string> GetSkillByNameAsync(string skillName);

        /**************************************************************/
        /// <summary>
        /// Retrieves a list of all available skill names.
        /// </summary>
        /// <returns>
        /// A task that resolves to a list of skill names that can be loaded.
        /// </returns>
        Task<List<string>> GetAvailableSkillsAsync();

        #endregion

        #region document retrieval methods

        /**************************************************************/
        /// <summary>
        /// Retrieves the capability contracts document (skills.md) from the refactored architecture.
        /// This document defines stable capability contracts without implementation details.
        /// </summary>
        /// <returns>
        /// A task that resolves to the capability contracts content as a string.
        /// </returns>
        /// <remarks>
        /// The capability contracts document describes WHAT the system can do, not HOW.
        /// Use this for high-level capability understanding and regulatory review.
        /// </remarks>
        /// <seealso cref="GetSelectorsDocumentAsync"/>
        /// <seealso cref="GetInterfaceDocumentAsync"/>
        Task<string> GetCapabilityContractsAsync();

        /**************************************************************/
        /// <summary>
        /// Retrieves the selectors document (selectors.md) containing skill routing rules.
        /// </summary>
        /// <returns>
        /// A task that resolves to the selectors document content as a string.
        /// </returns>
        /// <remarks>
        /// The selectors document contains decision trees, keyword mappings, and priority rules
        /// for routing user queries to appropriate skills.
        /// </remarks>
        /// <seealso cref="GetCapabilityContractsAsync"/>
        Task<string> GetSelectorsDocumentAsync();

        /**************************************************************/
        /// <summary>
        /// Retrieves a specific interface mapping document by skill name.
        /// Interface documents contain API endpoints, workflows, and output mappings.
        /// </summary>
        /// <param name="skillName">
        /// The skill name to retrieve the interface for (e.g., "indicationDiscovery", "labelContent").
        /// </param>
        /// <returns>
        /// A task that resolves to the interface document content as a string,
        /// or an error message if the interface is not found.
        /// </returns>
        /// <remarks>
        /// Interface documents map capability contracts to actual API implementations.
        /// They contain endpoint specifications, parameter details, and workflow patterns.
        /// </remarks>
        /// <seealso cref="GetCapabilityContractsAsync"/>
        Task<string> GetInterfaceDocumentAsync(string skillName);

        /**************************************************************/
        /// <summary>
        /// Retrieves the response format standards document.
        /// </summary>
        /// <returns>
        /// A task that resolves to the response format document content as a string.
        /// </returns>
        /// <remarks>
        /// Contains output requirements, label link formats, and data source rules.
        /// </remarks>
        Task<string> GetResponseFormatDocumentAsync();

        /**************************************************************/
        /// <summary>
        /// Retrieves the synthesis rules document containing content quality and aggregation rules.
        /// </summary>
        /// <returns>
        /// A task that resolves to the synthesis rules document content as a string.
        /// </returns>
        /// <remarks>
        /// Contains truncation detection, 404 handling, and multi-product aggregation rules.
        /// </remarks>
        /// <seealso cref="GetResponseFormatDocumentAsync"/>
        Task<string> GetSynthesisRulesDocumentAsync();

        /**************************************************************/
        /// <summary>
        /// Builds the complete skills document by loading and combining all skill files.
        /// </summary>
        /// <returns>
        /// A task that resolves to the complete skills document as a formatted string.
        /// </returns>
        /// <remarks>
        /// This method loads all skill content for comprehensive prompts. For token efficiency,
        /// prefer using <see cref="SelectSkillsAsync"/> and <see cref="GetSkillContentAsync"/>
        /// to load only the skills needed for a specific query.
        /// </remarks>
        /// <seealso cref="GetSkillContentAsync"/>
        Task<string> GetFullSkillsDocumentAsync();

        #endregion
    }

    #endregion

    #region skill selection models

    /**************************************************************/
    /// <summary>
    /// Represents the result of skill selection during Stage 1 of two-stage routing.
    /// </summary>
    /// <remarks>
    /// Contains information about which skills should be loaded for a given query,
    /// including confidence levels and whether the query can be answered directly
    /// without loading additional skills.
    /// </remarks>
    /// <seealso cref="IClaudeSkillService.SelectSkillsAsync"/>
    public class SkillSelection
    {
        /**************************************************************/
        /// <summary>
        /// Gets or sets the list of skill names that should be loaded.
        /// </summary>
        /// <remarks>
        /// Skill names correspond to the keys in the skill manifest (e.g., "label", "settings").
        /// </remarks>
        public List<string> SelectedSkills { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets a value indicating whether the query can be answered directly
        /// without loading full skill content.
        /// </summary>
        /// <remarks>
        /// When true, the <see cref="DirectResponse"/> property contains the response.
        /// </remarks>
        public bool IsDirectResponse { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the direct response when <see cref="IsDirectResponse"/> is true.
        /// </summary>
        public string? DirectResponse { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the explanation for the skill selection decision.
        /// </summary>
        public string? Explanation { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets a value indicating whether the selection was successful.
        /// </summary>
        public bool Success { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the error message if selection failed.
        /// </summary>
        public string? Error { get; set; }
    }

    #endregion

    #region skill service implementation

    /**************************************************************/
    /// <summary>
    /// Implementation of the skill service that manages AI skill documents for the Claude API.
    /// Implements a two-stage routing pattern for efficient skill loading.
    /// </summary>
    /// <remarks>
    /// The service reads skill files from the configured paths in appsettings.json and
    /// caches them for performance. The skill manifest is built from the individual skill
    /// files' headers and descriptions.
    /// </remarks>
    /// <seealso cref="IClaudeSkillService"/>
    public class ClaudeSkillService : IClaudeSkillService
    {
        #region private fields

        /**************************************************************/
        /// <summary>
        /// Configuration provider for skill file paths.
        /// </summary>
        private readonly IConfiguration _configuration;

        /**************************************************************/
        /// <summary>
        /// Logger instance for diagnostic output.
        /// </summary>
        private readonly ILogger<ClaudeSkillService> _logger;

        /**************************************************************/
        /// <summary>
        /// Service scope factory for creating isolated scopes to resolve dependencies.
        /// </summary>
        /// <remarks>
        /// Used to break the circular dependency between <see cref="ClaudeSkillService"/> and
        /// <see cref="IClaudeApiService"/>. By creating a new scope, we avoid the DI container
        /// failing during resolution when both services depend on each other.
        /// </remarks>
        /// <seealso cref="IClaudeApiService"/>
        /// <seealso cref="selectSkillsViaAiAsync"/>
        private readonly IServiceScopeFactory _serviceScopeFactory;

        /**************************************************************/
        /// <summary>
        /// Cached skill manifest content.
        /// </summary>
        private string? _manifestCache;

        /**************************************************************/
        /// <summary>
        /// Timestamp of last manifest cache refresh.
        /// </summary>
        private DateTime _manifestCacheTimestamp = DateTime.MinValue;

        /**************************************************************/
        /// <summary>
        /// Cache duration for skill documents (8 hours).
        /// </summary>
        private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(8);

        /**************************************************************/
        /// <summary>
        /// Configuration section name for skill file paths.
        /// </summary>
        private const string SkillConfigSection = "ClaudeApiSettings";

        /**************************************************************/
        /// <summary>
        /// Mapping of skill names to their configuration keys.
        /// </summary>
        /// <remarks>
        /// These keys correspond to entries in appsettings.json under ClaudeApiSettings.
        /// Uses case-insensitive comparison to handle variations in skill name casing.
        /// </remarks>
        private readonly Dictionary<string, string> _skillConfigKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            { "label", "Skill-Label" },
            { "section", "Skill-Section" },
            { "settings", "Skill-Settings" },
            { "userActivity", "Skill-UserActivity" },
            { "synthesis", "Skill-Synthesis" },
            { "retry", "Skill-Retry" },
            { "rescueWorkflow", "Skill-RescueWorkflow" },
            { "labelIndicationWorkflow", "Skill-LabelIndicationWorkflow" },
            { "labelProductIndication", "Skill-LabelProductIndication" },
            { "general", "Skill-General" },
            { "equianalgesicConversion", "Skill-EquianalgesicConversion" },
            { "pharmacologicClassSearch", "Skill-PharmacologicClassSearch" }
        };

        /**************************************************************/
        /// <summary>
        /// Mapping of skill names to their interface document paths.
        /// </summary>
        /// <remarks>
        /// These paths are relative to the Skills directory and map capability contracts
        /// to their implementation specifications. Interface documents contain API endpoints,
        /// workflows, and output mappings.
        /// </remarks>
        private readonly Dictionary<string, string> _interfaceDocPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            // Primary interface document mappings
            { "indicationDiscovery", "Skills/interfaces/api/indication-discovery.md" },
            { "labelContent", "Skills/interfaces/api/label-content.md" },
            { "equianalgesicConversion", "Skills/interfaces/api/equianalgesic-conversion.md" },
            { "userActivity", "Skills/interfaces/api/user-activity.md" },
            { "cacheManagement", "Skills/interfaces/api/cache-management.md" },
            { "sessionManagement", "Skills/interfaces/api/session-management.md" },
            { "dataRescue", "Skills/interfaces/api/data-rescue.md" },
            { "retryFallback", "Skills/interfaces/api/retry-fallback.md" },
            { "pharmacologicClass", "Skills/interfaces/api/pharmacologic-class.md" },
            { "pharmacologicClassSearch", "Skills/interfaces/api/pharmacologic-class.md" },

            // Alias mappings for skill names used in AI selection
            // These map the skill names from _skillConfigKeys to their interface documents
            { "label", "Skills/interfaces/api/label-content.md" },
            { "settings", "Skills/interfaces/api/cache-management.md" },
            { "rescueWorkflow", "Skills/interfaces/api/data-rescue.md" },
            { "labelIndicationWorkflow", "Skills/interfaces/api/indication-discovery.md" },
            { "labelProductIndication", "Skills/interfaces/api/indication-discovery.md" },
            { "retry", "Skills/interfaces/api/retry-fallback.md" },
            { "section", "Skills/interfaces/api/label-content.md" },
            { "synthesis", "Skills/interfaces/api/label-content.md" },
            { "general", "Skills/interfaces/api/session-management.md" }
        };

        /**************************************************************/
        /// <summary>
        /// Path to the synthesis rules document.
        /// </summary>
        /// <remarks>
        /// Contains content quality detection, aggregation rules, and 404 handling guidelines.
        /// </remarks>
        private const string SynthesisRulesPath = "Skills/interfaces/synthesis-rules.md";

        /**************************************************************/
        /// <summary>
        /// Path to the capability contracts document (skills.md).
        /// </summary>
        private const string CapabilityContractsPath = "Skills/skills.md";

        /**************************************************************/
        /// <summary>
        /// Path to the selectors document containing skill routing rules.
        /// </summary>
        private const string SelectorsDocPath = "Skills/selectors.md";

        /**************************************************************/
        /// <summary>
        /// Path to the response format standards document.
        /// </summary>
        private const string ResponseFormatPath = "Skills/interfaces/response-format.md";

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the ClaudeSkillService.
        /// </summary>
        /// <param name="configuration">Configuration provider for skill file paths.</param>
        /// <param name="logger">Logger instance for diagnostic output.</param>
        /// <param name="serviceScopeFactory">
        /// Service scope factory for creating isolated scopes to resolve <see cref="IClaudeApiService"/>.
        /// This breaks the circular dependency between skill and API services.
        /// </param>
        /// <seealso cref="IClaudeApiService"/>
        /// <seealso cref="selectSkillsViaAiAsync"/>
        public ClaudeSkillService(
            IConfiguration configuration,
            ILogger<ClaudeSkillService> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        }

        #endregion

        #region manifest methods

        /**************************************************************/
        /// <inheritdoc/>
        /// <remarks>
        /// The manifest is loaded from the selectors.md document which contains
        /// skill routing rules and decision trees for AI-based skill selection.
        /// </remarks>
        public async Task<string> GetSkillManifestAsync()
        {
            #region implementation

            // Cache key for manifest
            var cacheKey = "SkillManifest";

            // Check cache validity
            if (_manifestCache != null &&
                DateTime.UtcNow - _manifestCacheTimestamp < _cacheDuration)
            {
                return _manifestCache;
            }

            _logger.LogDebug("Loading skill manifest from selectors.md");

            // Load the selectors document as the manifest
            var selectorsContent = await GetSelectorsDocumentAsync();
            if (!selectorsContent.StartsWith("Skills document not found"))
            {
                _manifestCache = selectorsContent;
                _manifestCacheTimestamp = DateTime.UtcNow;
                return _manifestCache;
            }

            // Fallback to configured path
            var manifestPath = _configuration.GetValue<string>($"{SkillConfigSection}:Skill-Selector");
            if (!string.IsNullOrEmpty(manifestPath))
            {
                var content = readSkillFileByPath(manifestPath);
                if (!content.StartsWith("Skills document not found"))
                {
                    _manifestCache = content;
                    _manifestCacheTimestamp = DateTime.UtcNow;
                    return _manifestCache;
                }
            }

            // Return error message if no manifest found
            return "Skills manifest not found. Please ensure selectors.md exists in the Skills folder.";

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        /// <remarks>
        /// This method uses AI-based skill selection via <see cref="IClaudeApiService.SelectSkillsViaAiAsync"/>
        /// which interprets the selectors.md document to select appropriate skills dynamically.
        /// </remarks>
        /// <seealso cref="IClaudeApiService.SelectSkillsViaAiAsync"/>
        /// <seealso cref="selectSkillsViaAiAsync"/>
        public async Task<SkillSelection> SelectSkillsAsync(string userMessage, object? systemContext = null)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return new SkillSelection
                {
                    Success = false,
                    Error = "User message cannot be empty."
                };
            }

            _logger.LogDebug("Selecting skills for message: {MessagePreview}",
                userMessage.Length > 100 ? userMessage[..100] + "..." : userMessage);

            try
            {
                // Use AI-based skill selection with selectors.md
                return await selectSkillsViaAiAsync(userMessage, systemContext as AiSystemContext);
            }
            catch (Exception ex)
            {
                // Log the full exception details for debugging
                _logger.LogError(ex, "Error during skill selection for message: {MessagePreview}",
                    userMessage.Length > 100 ? userMessage[..100] + "..." : userMessage);

                // Fallback to loading default skill on error, but report the failure
                return new SkillSelection
                {
                    Success = false,
                    Error = $"Skill selection failed: {ex.Message}",
                    SelectedSkills = new List<string> { "label" },
                    Explanation = $"Defaulting to label skill due to selection error: {ex.Message}"
                };
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Performs AI-based skill selection using the selectors document and Claude API.
        /// </summary>
        /// <param name="userMessage">The user's natural language query.</param>
        /// <param name="systemContext">Optional system context for authentication state.</param>
        /// <returns>A <see cref="SkillSelection"/> with AI-selected skills.</returns>
        /// <remarks>
        /// This method creates an isolated service scope to resolve <see cref="IClaudeApiService"/>,
        /// breaking the circular dependency between <see cref="ClaudeSkillService"/> and
        /// <see cref="IClaudeApiService"/>. The scoped resolution ensures the DI container
        /// can properly instantiate the service without encountering resolution failures.
        ///
        /// Falls back to default "label" skill if AI selection fails, with detailed error logging.
        /// </remarks>
        /// <seealso cref="SelectSkillsAsync"/>
        /// <seealso cref="IClaudeApiService.SelectSkillsViaAiAsync"/>
        /// <seealso cref="_serviceScopeFactory"/>
        private async Task<SkillSelection> selectSkillsViaAiAsync(string userMessage, AiSystemContext? systemContext)
        {
            #region implementation

            _logger.LogDebug("Using AI-based skill selection");

            // Load the selectors document
            var selectorsDocument = await GetSelectorsDocumentAsync();

            if (selectorsDocument.StartsWith("Skills document not found"))
            {
                _logger.LogWarning("Selectors document not found, using default label skill");
                return new SkillSelection
                {
                    Success = false,
                    Error = "Selectors document not found.",
                    SelectedSkills = new List<string> { "label" },
                    Explanation = "Selectors document not found - using default skill."
                };
            }

            // Create a new scope to resolve IClaudeApiService, breaking circular dependency
            // between ClaudeSkillService and ClaudeApiService
            using var scope = _serviceScopeFactory.CreateScope();
            var claudeApiService = scope.ServiceProvider.GetRequiredService<IClaudeApiService>();

            // Call the Claude API service for AI-based selection
            SkillSelectionResult aiResult = await claudeApiService.SelectSkillsViaAiAsync(
                userMessage,
                selectorsDocument,
                systemContext);

            if (!aiResult.Success || aiResult.SelectedSkills.Count == 0)
            {
                _logger.LogWarning("AI skill selection failed or returned empty, using default. Error: {Error}",
                    aiResult.Error);
                return new SkillSelection
                {
                    Success = false,
                    Error = aiResult.Error ?? "AI selection returned empty results.",
                    SelectedSkills = new List<string> { "label" },
                    Explanation = $"AI selection error: {aiResult.Error} - using default skill."
                };
            }

            // Map AI skill names to internal skill names
            List<string>? mappedSkills = mapAiSkillNamesToInternal(aiResult.SelectedSkills);

            _logger.LogInformation("[AI SKILL SELECTION] Selected skills: [{Skills}]",
                string.Join(", ", mappedSkills));

            return new SkillSelection
            {
                Success = true,
                SelectedSkills = mappedSkills,
                IsDirectResponse = aiResult.IsDirectResponse,
                DirectResponse = aiResult.DirectResponse,
                Explanation = aiResult.Explanation ?? $"AI selected {mappedSkills.Count} skill(s)."
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Maps AI-selected skill names from selectors.md to internal skill configuration keys.
        /// </summary>
        /// <param name="aiSkillNames">Skill names as returned by AI selection.</param>
        /// <returns>Mapped skill names compatible with internal skill loading.</returns>
        /// <remarks>
        /// The selectors.md uses skill names like "indicationDiscovery" and "labelContent",
        /// while the internal configuration uses keys like "labelIndicationWorkflow" and "label".
        /// This method performs the necessary mapping.
        /// </remarks>
        private List<string> mapAiSkillNamesToInternal(List<string> aiSkillNames)
        {
            #region implementation

            // Mapping from selectors.md skill names to internal config keys
            var skillNameMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "indicationDiscovery", "labelIndicationWorkflow" },
                { "labelContent", "label" },
                { "equianalgesicConversion", "equianalgesicConversion" },
                { "userActivity", "userActivity" },
                { "cacheManagement", "settings" },
                { "sessionManagement", "general" },
                { "dataRescue", "rescueWorkflow" },
                { "retryFallback", "retry" },
                { "pharmacologicClass", "pharmacologicClassSearch" },
                { "pharmacologicClassSearch", "pharmacologicClassSearch" }
            };

            var mappedSkills = new List<string>();

            foreach (var aiName in aiSkillNames)
            {
                if (skillNameMapping.TryGetValue(aiName, out var internalName))
                {
                    if (!mappedSkills.Contains(internalName))
                    {
                        mappedSkills.Add(internalName);
                    }
                }
                else
                {
                    // If no mapping found, use as-is (might be a direct match)
                    if (!mappedSkills.Contains(aiName))
                    {
                        mappedSkills.Add(aiName);
                    }
                }
            }

            // Ensure at least one skill is selected
            if (mappedSkills.Count == 0)
            {
                mappedSkills.Add("label");
            }

            return mappedSkills;

            #endregion
        }

        #endregion

        #region skill loading methods

        /**************************************************************/
        /// <inheritdoc/>
        /// <remarks>
        /// This method builds the skill content by:
        /// 1. Prefixing skills.md (capability contracts) at the beginning of the content
        /// 2. Loading interface documents for each selected skill (contains API endpoints and workflows)
        /// 3. Appending response-format.md document (contains output requirements)
        /// 4. Appending synthesis-rules.md document (contains content quality rules)
        /// This ensures Claude has complete instructions for generating proper responses with
        /// label links and data source attribution.
        /// </remarks>
        /// <seealso cref="GetCapabilityContractsAsync"/>
        /// <seealso cref="GetInterfaceDocumentAsync"/>
        /// <seealso cref="GetResponseFormatDocumentAsync"/>
        /// <seealso cref="GetSynthesisRulesDocumentAsync"/>
        public async Task<string> GetSkillContentAsync(SkillSelection selection)
        {
            #region implementation

            if (selection == null || !selection.SelectedSkills.Any())
            {
                _logger.LogWarning("No skills selected, returning label skill as default");
                return await GetSkillByNameAsync("label");
            }

            var sb = new System.Text.StringBuilder();

            // Prefix with capability contracts (skills.md)
            // This provides the foundational context about what the system can do
            var capabilityContracts = await GetCapabilityContractsAsync();
            if (!string.IsNullOrEmpty(capabilityContracts) &&
                !capabilityContracts.StartsWith("Skills document not found") &&
                !capabilityContracts.StartsWith("Capability contracts document"))
            {
                sb.AppendLine("=== CAPABILITY CONTRACTS (skills.md) ===");
                sb.AppendLine(capabilityContracts);
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }

            // Load each selected skill and its corresponding interface document
            foreach (var skillName in selection.SelectedSkills)
            {
                var content = await GetSkillByNameAsync(skillName);
                if (!string.IsNullOrEmpty(content) && !content.StartsWith("Skill"))
                {
                    if (sb.Length > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("---");
                        sb.AppendLine();
                    }
                    sb.AppendLine(content);

                    // Also load the interface document for this skill
                    var interfaceContent = await GetInterfaceDocumentAsync(skillName);
                    if (!string.IsNullOrEmpty(interfaceContent) && !interfaceContent.StartsWith("Interface document"))
                    {
                        sb.AppendLine();
                        sb.AppendLine("---");
                        sb.AppendLine();
                        sb.AppendLine(interfaceContent);
                    }
                }
            }

            // Append response format standards
            var responseFormat = await GetResponseFormatDocumentAsync();
            if (!string.IsNullOrEmpty(responseFormat) && !responseFormat.StartsWith("Skills document not found"))
            {
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine(responseFormat);
            }

            // Append synthesis rules
            var synthesisRules = await GetSynthesisRulesDocumentAsync();
            if (!string.IsNullOrEmpty(synthesisRules) && !synthesisRules.StartsWith("Synthesis rules document"))
            {
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine(synthesisRules);
            }

            _logger.LogInformation("[SKILL CONTENT] Loaded skills [{Skills}]",
                string.Join(", ", selection.SelectedSkills));

            return sb.ToString();

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        /// <remarks>
        /// If the skill file is not found in the configured path, attempts to return
        /// the interface document instead. This allows the architecture to work where
        /// skill files may not exist in the root skills folder.
        /// </remarks>
        /// <seealso cref="GetInterfaceDocumentAsync"/>
        public async Task<string> GetSkillByNameAsync(string skillName)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(skillName))
            {
                return "Skill name cannot be empty.";
            }

            var normalizedName = skillName.ToLowerInvariant().Trim();

            // Try direct lookup first
            if (_skillConfigKeys.TryGetValue(normalizedName, out var configKey))
            {
                var content = readSkillFile(configKey, $"GetSkillByName_{normalizedName}");

                // If skill file not found or config not found, try to use interface document
                // readSkillFile returns either "Skills document not found" or "{configKey} configuration not found"
                if (content.StartsWith("Skills document not found") ||
                    content.Contains("configuration not found"))
                {
                    _logger.LogDebug("Skill file not found for {SkillName}, trying interface document", skillName);
                    var interfaceContent = await GetInterfaceDocumentAsync(normalizedName);
                    if (!interfaceContent.StartsWith("Interface document"))
                    {
                        return interfaceContent;
                    }
                }

                return content;
            }

            // Try fuzzy matching
            var matchedKey = _skillConfigKeys.Keys
                .FirstOrDefault(k => k.Contains(normalizedName) || normalizedName.Contains(k));

            if (matchedKey != null)
            {
                var content = readSkillFile(_skillConfigKeys[matchedKey], $"GetSkillByName_{matchedKey}");

                // If skill file not found or config not found, try to use interface document
                // readSkillFile returns either "Skills document not found" or "{configKey} configuration not found"
                if (content.StartsWith("Skills document not found") ||
                    content.Contains("configuration not found"))
                {
                    _logger.LogDebug("Skill file not found for {SkillName} (matched to {MatchedKey}), trying interface document",
                        skillName, matchedKey);
                    var interfaceContent = await GetInterfaceDocumentAsync(matchedKey);
                    if (!interfaceContent.StartsWith("Interface document"))
                    {
                        return interfaceContent;
                    }
                }

                return content;
            }

            return $"Skill '{skillName}' not found. Available skills: {string.Join(", ", _skillConfigKeys.Keys)}";

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<string>> GetAvailableSkillsAsync()
        {
            return Task.FromResult(_skillConfigKeys.Keys.ToList());
        }

        #endregion

        #region document retrieval methods

        /**************************************************************/
        /// <inheritdoc/>
        /// <remarks>
        /// Loads all primary skills into a single document.
        /// </remarks>
        public async Task<string> GetFullSkillsDocumentAsync()
        {
            #region implementation

            var key = "ClaudeSkillService_GetFullSkillsDocument".Base64Encode();
            var cached = PerformanceHelper.GetCache<string>(key);

            if (!string.IsNullOrEmpty(cached))
            {
                return cached;
            }

            _logger.LogDebug("Building full skills document");

            var sb = new System.Text.StringBuilder();

            // Load label skill (primary skill document)
            var labelSkill = await GetSkillByNameAsync("label");
            if (!string.IsNullOrEmpty(labelSkill) && !labelSkill.StartsWith("Skill"))
            {
                sb.AppendLine(labelSkill);
            }

            // Append settings skills
            var settingsSkill = await GetSkillByNameAsync("settings");
            if (!string.IsNullOrEmpty(settingsSkill) && !settingsSkill.StartsWith("Skill"))
            {
                sb.AppendLine();
                sb.AppendLine(settingsSkill);
            }

            // Append user activity skills
            var userActivitySkill = await GetSkillByNameAsync("userActivity");
            if (!string.IsNullOrEmpty(userActivitySkill) && !userActivitySkill.StartsWith("Skill"))
            {
                sb.AppendLine();
                sb.AppendLine(userActivitySkill);
            }

            var result = sb.ToString();

            // Cache for 8 hours
            PerformanceHelper.SetCacheManageKey(key, result, 8);

            return result;

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        /// <seealso cref="GetSelectorsDocumentAsync"/>
        /// <seealso cref="GetInterfaceDocumentAsync"/>
        public Task<string> GetCapabilityContractsAsync()
        {
            #region implementation

            var key = "ClaudeSkillService_CapabilityContracts".Base64Encode();
            var cached = PerformanceHelper.GetCache<string>(key);

            if (!string.IsNullOrEmpty(cached))
            {
                return Task.FromResult(cached);
            }

            _logger.LogDebug("Loading capability contracts from {Path}", CapabilityContractsPath);

            var content = readSkillFileByPath(CapabilityContractsPath);

            // Cache for 8 hours
            if (!content.StartsWith("Skills document not found"))
            {
                PerformanceHelper.SetCacheManageKey(key, content, 8);
            }

            return Task.FromResult(content);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        /// <remarks>
        /// Returns Skills/selectors.md containing skill routing rules and decision trees.
        /// </remarks>
        /// <seealso cref="GetCapabilityContractsAsync"/>
        /// <seealso cref="GetInterfaceDocumentAsync"/>
        public Task<string> GetSelectorsDocumentAsync()
        {
            #region implementation

            var key = "ClaudeSkillService_SelectorsDoc".Base64Encode();
            var cached = PerformanceHelper.GetCache<string>(key);

            if (!string.IsNullOrEmpty(cached))
            {
                return Task.FromResult(cached);
            }

            _logger.LogDebug("Loading selectors document from {Path}", SelectorsDocPath);

            var content = readSkillFileByPath(SelectorsDocPath);

            // Cache for 8 hours
            if (!content.StartsWith("Skills document not found"))
            {
                PerformanceHelper.SetCacheManageKey(key, content, 8);
            }

            return Task.FromResult(content);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        /// <seealso cref="GetCapabilityContractsAsync"/>
        /// <seealso cref="GetSelectorsDocumentAsync"/>
        public Task<string> GetInterfaceDocumentAsync(string skillName)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(skillName))
            {
                return Task.FromResult("Skill name cannot be empty.");
            }

            var normalizedName = skillName.Trim();

            // Try direct lookup first
            if (_interfaceDocPaths.TryGetValue(normalizedName, out var docPath))
            {
                var key = $"ClaudeSkillService_Interface_{normalizedName}".Base64Encode();
                var cached = PerformanceHelper.GetCache<string>(key);

                if (!string.IsNullOrEmpty(cached))
                {
                    return Task.FromResult(cached);
                }

                _logger.LogDebug("Loading interface document for {SkillName} from {Path}", normalizedName, docPath);

                var content = readSkillFileByPath(docPath);

                // Cache for 8 hours
                if (!content.StartsWith("Skills document not found"))
                {
                    PerformanceHelper.SetCacheManageKey(key, content, 8);
                }

                return Task.FromResult(content);
            }

            // Try fuzzy matching
            var matchedKey = _interfaceDocPaths.Keys
                .FirstOrDefault(k => k.Contains(normalizedName, StringComparison.OrdinalIgnoreCase) ||
                                     normalizedName.Contains(k, StringComparison.OrdinalIgnoreCase));

            if (matchedKey != null)
            {
                return GetInterfaceDocumentAsync(matchedKey);
            }

            return Task.FromResult(
                $"Interface document for '{skillName}' not found. " +
                $"Available interfaces: {string.Join(", ", _interfaceDocPaths.Keys)}");

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<string> GetResponseFormatDocumentAsync()
        {
            #region implementation

            var key = "ClaudeSkillService_ResponseFormat".Base64Encode();
            var cached = PerformanceHelper.GetCache<string>(key);

            if (!string.IsNullOrEmpty(cached))
            {
                return Task.FromResult(cached);
            }

            _logger.LogDebug("Loading response format document from {Path}", ResponseFormatPath);

            var content = readSkillFileByPath(ResponseFormatPath);

            // Cache for 8 hours
            if (!content.StartsWith("Skills document not found"))
            {
                PerformanceHelper.SetCacheManageKey(key, content, 8);
            }

            return Task.FromResult(content);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        /// <seealso cref="GetResponseFormatDocumentAsync"/>
        public Task<string> GetSynthesisRulesDocumentAsync()
        {
            #region implementation

            var key = "ClaudeSkillService_SynthesisRules".Base64Encode();
            var cached = PerformanceHelper.GetCache<string>(key);

            if (!string.IsNullOrEmpty(cached))
            {
                return Task.FromResult(cached);
            }

            _logger.LogDebug("Loading synthesis rules document from {Path}", SynthesisRulesPath);

            var content = readSkillFileByPath(SynthesisRulesPath);

            // Cache for 8 hours
            if (!content.StartsWith("Skills document not found"))
            {
                PerformanceHelper.SetCacheManageKey(key, content, 8);
            }

            return Task.FromResult(content);

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Reads a skill file from the configured path with caching support.
        /// </summary>
        /// <param name="configKey">The configuration key (e.g., "Skill-Label", "Skill-Settings").</param>
        /// <param name="cacheKeyPrefix">A prefix for the cache key.</param>
        /// <returns>The skill file content as a string.</returns>
        /// <remarks>
        /// Reads from ClaudeApiSettings configuration section.
        /// </remarks>
        private string readSkillFile(string configKey, string cacheKeyPrefix)
        {
            #region implementation

            var key = $"{cacheKeyPrefix}_{configKey}".Base64Encode();
            var cachedSkills = PerformanceHelper.GetCache<string>(key);

            if (!string.IsNullOrEmpty(cachedSkills))
            {
                return cachedSkills;
            }

            // Get the configured path from ClaudeApiSettings
            var skillFilePath = _configuration.GetValue<string>($"{SkillConfigSection}:{configKey}");

            if (string.IsNullOrEmpty(skillFilePath))
            {
                return $"{configKey} configuration not found in {SkillConfigSection}.";
            }

            var content = readSkillFileByPath(skillFilePath);

            // Cache for 8 hours to reduce file I/O
            PerformanceHelper.SetCacheManageKey(key, content, 8);

            return content;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Reads a skill file from the specified path.
        /// </summary>
        /// <param name="skillFilePath">The relative or absolute path to the skill file.</param>
        /// <returns>The file content or an error message.</returns>
        private string readSkillFileByPath(string skillFilePath)
        {
            #region implementation

            // Resolve the path relative to the application's content root
            var fullPath = Path.Combine(AppContext.BaseDirectory, skillFilePath);

            if (!File.Exists(fullPath))
            {
                // Try relative to current directory as fallback
                fullPath = Path.Combine(Directory.GetCurrentDirectory(), skillFilePath);
            }

            if (!File.Exists(fullPath))
            {
                return $"Skills document not found at: {skillFilePath}";
            }

            return File.ReadAllText(fullPath);

            #endregion
        }

        #endregion
    }

    #endregion
}
