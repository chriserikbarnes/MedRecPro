using MedRecPro.Helpers;
using Microsoft.Extensions.Configuration;
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

        #region legacy compatibility methods

        /**************************************************************/
        /// <summary>
        /// Builds the complete skills document by loading and combining all skill files.
        /// This method provides backward compatibility with the original monolithic approach.
        /// </summary>
        /// <returns>
        /// A task that resolves to the complete skills document as a formatted string.
        /// </returns>
        /// <remarks>
        /// This method is retained for backward compatibility but should be avoided for
        /// new code. Use the two-stage routing pattern with <see cref="SelectSkillsAsync"/>
        /// and <see cref="GetSkillContentAsync"/> instead.
        /// </remarks>
        /// <seealso cref="GetSkillContentAsync"/>
        Task<string> GetFullSkillsDocumentAsync();

        /**************************************************************/
        /// <summary>
        /// Retrieves the label section prompt skills document.
        /// </summary>
        /// <returns>The label section content query prompt skills.</returns>
        string GetLabelSectionPromptSkills();

        /**************************************************************/
        /// <summary>
        /// Retrieves the synthesis prompt skills document.
        /// </summary>
        /// <returns>The synthesis prompt skills.</returns>
        string GetSynthesisPromptSkills();

        /**************************************************************/
        /// <summary>
        /// Retrieves the retry prompt skills document.
        /// </summary>
        /// <returns>The retry prompt skills.</returns>
        string GetRetryPromptSkills();

        #endregion

        #region refactored architecture methods

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
            { "equianalgesicConversion", "Skill-EquianalgesicConversion" }
        };

        /**************************************************************/
        /// <summary>
        /// Mapping of skill names to their interface document paths in the refactored architecture.
        /// </summary>
        /// <remarks>
        /// These paths are relative to the Skills directory and map capability contracts
        /// to their implementation specifications. Interface documents contain API endpoints,
        /// workflows, and output mappings.
        /// </remarks>
        private readonly Dictionary<string, string> _interfaceDocPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            { "indicationDiscovery", "Skills/interfaces/api/indication-discovery.md" },
            { "labelContent", "Skills/interfaces/api/label-content.md" },
            { "equianalgesicConversion", "Skills/interfaces/api/equianalgesic-conversion.md" },
            { "userActivity", "Skills/interfaces/api/user-activity.md" },
            { "cacheManagement", "Skills/interfaces/api/cache-management.md" },
            { "sessionManagement", "Skills/interfaces/api/session-management.md" },
            { "dataRescue", "Skills/interfaces/api/data-rescue.md" }
        };

        /**************************************************************/
        /// <summary>
        /// Path to the capability contracts document in the refactored architecture.
        /// </summary>
        private const string CapabilityContractsPath = "Skills/skills.md";

        /**************************************************************/
        /// <summary>
        /// Path to the selectors document in the refactored architecture.
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
        public ClaudeSkillService(
            IConfiguration configuration,
            ILogger<ClaudeSkillService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region manifest methods

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<string> GetSkillManifestAsync()
        {
            #region implementation

            // Check cache validity
            if (_manifestCache != null &&
                DateTime.UtcNow - _manifestCacheTimestamp < _cacheDuration)
            {
                return _manifestCache;
            }

            _logger.LogDebug("Building skill manifest");

            // Load the skill selector manifest from file
            var manifestPath = _configuration.GetValue<string>("ClaudeApiSettings:Skill-Selector");
            if (!string.IsNullOrEmpty(manifestPath))
            {
                var content = readSkillFileByPath(manifestPath);
                if (!content.StartsWith("Skills document not found") && !content.StartsWith("Skill-Selector"))
                {
                    _manifestCache = content;
                    _manifestCacheTimestamp = DateTime.UtcNow;
                    return _manifestCache;
                }
            }

            // Fallback: build manifest from available skills
            _manifestCache = await buildSkillManifestAsync();
            _manifestCacheTimestamp = DateTime.UtcNow;

            return _manifestCache;

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
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
                // Perform keyword-based skill selection (fast, no API call needed)
                var selectedSkills = performKeywordSkillSelection(userMessage);

                return new SkillSelection
                {
                    Success = true,
                    SelectedSkills = selectedSkills,
                    Explanation = $"Selected {selectedSkills.Count} skill(s) based on query analysis."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during skill selection");

                // Fallback to loading all skills on error
                return new SkillSelection
                {
                    Success = true,
                    SelectedSkills = new List<string> { "label" },
                    Explanation = "Defaulting to label skill due to selection error."
                };
            }

            #endregion
        }

        #endregion

        #region skill loading methods

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<string> GetSkillContentAsync(SkillSelection selection)
        {
            #region implementation

            if (selection == null || !selection.SelectedSkills.Any())
            {
                _logger.LogWarning("No skills selected, returning label skill as default");
                return await GetSkillByNameAsync("label");
            }

            var sb = new System.Text.StringBuilder();

            foreach (var skillName in selection.SelectedSkills)
            {
                var content = await GetSkillByNameAsync(skillName);
                if (!string.IsNullOrEmpty(content))
                {
                    if (sb.Length > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("---");
                        sb.AppendLine();
                    }
                    sb.AppendLine(content);
                }
            }

            return sb.ToString();

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<string> GetSkillByNameAsync(string skillName)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(skillName))
            {
                return Task.FromResult("Skill name cannot be empty.");
            }

            var normalizedName = skillName.ToLowerInvariant().Trim();

            // Try direct lookup first
            if (_skillConfigKeys.TryGetValue(normalizedName, out var configKey))
            {
                return Task.FromResult(readSkillFile(configKey, $"GetSkillByName_{normalizedName}"));
            }

            // Try fuzzy matching
            var matchedKey = _skillConfigKeys.Keys
                .FirstOrDefault(k => k.Contains(normalizedName) || normalizedName.Contains(k));

            if (matchedKey != null)
            {
                return Task.FromResult(readSkillFile(_skillConfigKeys[matchedKey], $"GetSkillByName_{matchedKey}"));
            }

            return Task.FromResult($"Skill '{skillName}' not found. Available skills: {string.Join(", ", _skillConfigKeys.Keys)}");

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<string>> GetAvailableSkillsAsync()
        {
            return Task.FromResult(_skillConfigKeys.Keys.ToList());
        }

        #endregion

        #region legacy compatibility methods

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<string> GetFullSkillsDocumentAsync()
        {
            #region implementation

            var key = "ClaudeSkillService_GetFullSkillsDocument".Base64Encode();
            var cached = PerformanceHelper.GetCache<string>(key);

            if (!string.IsNullOrEmpty(cached))
            {
                return cached;
            }

            _logger.LogDebug("Building full skills document (legacy mode)");

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
        public string GetLabelSectionPromptSkills()
        {
            return readSkillFile("Skill-Section", "GetLabelSectionPromptSkills");
        }

        /**************************************************************/
        /// <inheritdoc/>
        public string GetSynthesisPromptSkills()
        {
            return readSkillFile("Skill-Synthesis", "GetSynthesisPromptSkills");
        }

        /**************************************************************/
        /// <inheritdoc/>
        public string GetRetryPromptSkills()
        {
            return readSkillFile("Skill-Retry", "GetRetryPromptSkills");
        }

        #endregion

        #region refactored architecture methods

        /**************************************************************/
        /// <inheritdoc/>
        /// <seealso cref="GetSelectorsDocumentAsync"/>
        /// <seealso cref="GetInterfaceDocumentAsync"/>
        public Task<string> GetCapabilityContractsAsync()
        {
            #region implementation

            var key = $"ClaudeSkillService_CapabilityContracts".Base64Encode();
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
        /// <seealso cref="GetCapabilityContractsAsync"/>
        /// <seealso cref="GetInterfaceDocumentAsync"/>
        public Task<string> GetSelectorsDocumentAsync()
        {
            #region implementation

            var key = $"ClaudeSkillService_SelectorsDoc".Base64Encode();
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

            var key = $"ClaudeSkillService_ResponseFormat".Base64Encode();
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

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Builds the skill manifest from available skill files.
        /// </summary>
        /// <returns>The manifest content as a formatted string.</returns>
        private async Task<string> buildSkillManifestAsync()
        {
            #region implementation

            var sb = new System.Text.StringBuilder();

            sb.AppendLine("# MedRecPro AI Skills Manifest");
            sb.AppendLine();
            sb.AppendLine("Select the appropriate skill(s) to handle the user's query. Each skill provides specialized capabilities.");
            sb.AppendLine();
            sb.AppendLine("## Available Skills");
            sb.AppendLine();

            // Label Indication Workflow skill - FIRST PRIORITY for condition-based queries
            sb.AppendLine("### labelIndicationWorkflow");
            sb.AppendLine("**Description**: Product discovery by medical condition, disease, symptom, or therapeutic need.");
            sb.AppendLine("**Use for**: Finding products for conditions (depression, hypertension, diabetes), symptom-based searches, treatment options, generic alternatives.");
            sb.AppendLine("**Keywords**: what helps with, I have, options for, treatment for, products for, what can I take, alternatives to, depression, anxiety, diabetes, hypertension, condition, symptom");
            sb.AppendLine("**Process**: 1) Load labelProductIndication.md reference data, 2) Match condition to UNII(s), 3) Call GetProductLatestLabels, 4) Call GetRelatedProducts");
            sb.AppendLine("**Note**: This is the FIRST-LINE approach for condition-based queries. Always load labelProductIndication with this skill.");
            sb.AppendLine();

            // Label skill - primary pharmaceutical labeling operations
            sb.AppendLine("### label");
            sb.AppendLine("**Description**: Pharmaceutical labeling management and SPL document operations.");
            sb.AppendLine("**Use for**: Product searches, ingredient queries, NDC lookups, document navigation, labeler/manufacturer searches, pharmacologic class queries, import/export operations, section content retrieval (side effects, warnings, dosage), drug information.");
            sb.AppendLine("**Keywords**: drug, product, ingredient, NDC, manufacturer, labeler, import, export, label, section, warning, side effect, dosage, interaction, SPL, document");
            sb.AppendLine();

            // User activity skill - monitoring and observability (includes logs)
            sb.AppendLine("### userActivity");
            sb.AppendLine("**Description**: User activity monitoring, application log viewing, and endpoint performance statistics.");
            sb.AppendLine("**Use for**: Application logs, log filtering, error investigation, user activity tracking, endpoint performance analysis, API response times.");
            sb.AppendLine("**Keywords**: log, logs, error, warning, debug, trace, activity, user activity, performance, endpoint, statistics, controller, response time, monitoring");
            sb.AppendLine();

            // Settings skill - cache management only
            sb.AppendLine("### settings");
            sb.AppendLine("**Description**: Cache management and system configuration.");
            sb.AppendLine("**Use for**: Clearing cache, cache invalidation, system configuration.");
            sb.AppendLine("**Keywords**: cache, clear cache, reset cache, flush cache, invalidate");
            sb.AppendLine();

            // Rescue workflow skill - fallback strategies for missing data
            sb.AppendLine("### rescueWorkflow");
            sb.AppendLine("**Description**: Fallback strategies when primary label queries return empty or incomplete results.");
            sb.AppendLine("**Use for**: Finding data in alternative document locations, extracting info from narrative text (e.g., inactive ingredients in Description section), rescue queries when structured data is unavailable.");
            sb.AppendLine("**Keywords**: not found, empty results, where else, alternative, rescue, fallback, text search, description section, inactive ingredient, excipient");
            sb.AppendLine("**Note**: Load this skill IN ADDITION to label skill when primary queries fail.");
            sb.AppendLine();

            sb.AppendLine("## Selection Instructions");
            sb.AppendLine();
            sb.AppendLine("1. **Check for condition/symptom queries FIRST** - If user asks 'what helps with X' or mentions conditions/symptoms, select 'labelIndicationWorkflow' + 'labelProductIndication'.");
            sb.AppendLine("2. Analyze the user's query for keywords matching the skill descriptions above.");
            sb.AppendLine("3. Select the most specific skill(s) needed - avoid loading unnecessary skills.");
            sb.AppendLine("4. For detailed label content (side effects, warnings, dosage), use the 'label' skill.");
            sb.AppendLine("5. Queries about logs, user activity, or performance use the 'userActivity' skill.");
            sb.AppendLine("6. Queries about cache operations use the 'settings' skill.");
            sb.AppendLine("7. If uncertain and not a condition-based query, default to 'label' skill.");
            sb.AppendLine();

            sb.AppendLine("## Response Format");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"selectedSkills\": [\"skill1\", \"skill2\"],");
            sb.AppendLine("  \"explanation\": \"Brief reason for selection\"");
            sb.AppendLine("}");
            sb.AppendLine("```");

            return sb.ToString();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Performs keyword-based skill selection using keywords from the skillSelector.md manifest.
        /// This ensures a single source of truth for skill selection logic.
        /// </summary>
        /// <param name="userMessage">The user's message to analyze.</param>
        /// <returns>List of skill names that should be loaded.</returns>
        /// <remarks>
        /// Keywords are loaded from skillSelector.md on first call and cached.
        /// The manifest contains keyword definitions for each skill under "**Keywords**:" lines.
        /// This approach consolidates skill selection logic to avoid duplication between
        /// code and documentation.
        /// </remarks>
        /// <seealso cref="GetSkillManifestAsync"/>
        /// <seealso cref="loadKeywordsFromManifest"/>
        private List<string> performKeywordSkillSelection(string userMessage)
        {
            #region implementation

            var message = userMessage.ToLowerInvariant();
            var selectedSkills = new List<string>();

            // Load keywords from skillSelector.md (cached after first load)
            var skillKeywords = loadKeywordsFromManifest();

            // Check for indication workflow skill FIRST (condition-based queries)
            // This takes priority for finding products by condition/symptom
            if (skillKeywords.TryGetValue("labelIndicationWorkflow", out var indicationWorkflowKeywords))
            {
                if (indicationWorkflowKeywords.Any(k => message.Contains(k)))
                {
                    selectedSkills.Add("labelIndicationWorkflow");
                    // Also load the reference data file for UNII matching
                    selectedSkills.Add("labelProductIndication");
                }
            }

            // Check for user activity/monitoring skill (logs + user activity + performance)
            if (skillKeywords.TryGetValue("userActivity", out var userActivityKeywords))
            {
                if (userActivityKeywords.Any(k => message.Contains(k)))
                {
                    selectedSkills.Add("userActivity");
                }
            }

            // Check for settings skill (cache only)
            if (skillKeywords.TryGetValue("settings", out var settingsKeywords))
            {
                if (settingsKeywords.Any(k => message.Contains(k)))
                {
                    selectedSkills.Add("settings");
                }
            }

            // Label skill for detailed section content queries
            // Add it if pharmaceutical terms are present and no indication workflow was selected
            if (skillKeywords.TryGetValue("label", out var labelKeywords))
            {
                // Define detailed label content keywords inline (these are specific to label sections)
                var needsDetailedLabelContent = new[] { "side effect", "warning", "dosage", "section", "adverse", "contraindication" };

                if (needsDetailedLabelContent.Any(k => message.Contains(k)))
                {
                    // Add label skill for detailed section content even if indication workflow was selected
                    if (!selectedSkills.Contains("label"))
                    {
                        selectedSkills.Add("label");
                    }
                }
                else if (labelKeywords.Any(k => message.Contains(k)) && !selectedSkills.Contains("labelIndicationWorkflow"))
                {
                    // Add label as primary skill only if indication workflow wasn't already selected
                    selectedSkills.Insert(0, "label");
                }
                else if (selectedSkills.Count == 0)
                {
                    // Default to label skill if no other skills matched
                    selectedSkills.Add("label");
                }
            }
            else if (selectedSkills.Count == 0)
            {
                // Fallback if manifest couldn't be loaded - default to label skill
                selectedSkills.Add("label");
            }

            // Rescue workflow skill keywords - used when primary queries fail to find data
            if (skillKeywords.TryGetValue("rescueWorkflow", out var rescueWorkflowKeywords))
            {
                if (rescueWorkflowKeywords.Any(k => message.Contains(k)))
                {
                    selectedSkills.Add("rescueWorkflow");
                }
            }

            // General skill keywords - AI workflow, auth, user management
            if (skillKeywords.TryGetValue("general", out var generalKeywords))
            {
                if (generalKeywords.Any(k => message.Contains(k)))
                {
                    selectedSkills.Add("general");
                }
            }

            // Equianalgesic conversion skill keywords - opioid dose conversions
            if (skillKeywords.TryGetValue("equianalgesicConversion", out var equianalgesicKeywords))
            {
                var matchedKeyword = equianalgesicKeywords.FirstOrDefault(k => message.Contains(k));
                if (matchedKeyword != null)
                {
                    selectedSkills.Add("equianalgesicConversion");
                    _logger.LogInformation("[SKILL SELECTION] equianalgesicConversion matched on keyword: '{Keyword}'", matchedKeyword);
                    // Also add labelProductIndication for UNII lookups
                    if (!selectedSkills.Contains("labelProductIndication"))
                    {
                        selectedSkills.Add("labelProductIndication");
                    }
                }
            }

            // Remove duplicates and log final selection
            var finalSkills = selectedSkills.Distinct().ToList();
            _logger.LogInformation("[SKILL SELECTION] Final selected skills: [{Skills}] for query: '{QueryPreview}'",
                string.Join(", ", finalSkills),
                userMessage.Length > 80 ? userMessage[..80] + "..." : userMessage);
            return finalSkills;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Cached skill keywords loaded from skillSelector.md manifest.
        /// </summary>
        private Dictionary<string, List<string>>? _skillKeywordsCache;

        /**************************************************************/
        /// <summary>
        /// Timestamp of last skill keywords cache refresh.
        /// </summary>
        private DateTime _skillKeywordsCacheTimestamp = DateTime.MinValue;

        /**************************************************************/
        /// <summary>
        /// Loads skill keywords from the skillSelector.md manifest file.
        /// Keywords are extracted from "**Keywords**:" lines in each skill section.
        /// </summary>
        /// <returns>
        /// Dictionary mapping skill names to their keyword lists.
        /// Returns default keywords if manifest cannot be loaded.
        /// </returns>
        /// <remarks>
        /// This method reads the skillSelector.md file and parses keyword definitions.
        /// Results are cached for the same duration as other skill content.
        ///
        /// Expected manifest format:
        /// ### skillName
        /// **Keywords**: keyword1, keyword2, keyword phrase, ...
        /// </remarks>
        /// <seealso cref="GetSkillManifestAsync"/>
        private Dictionary<string, List<string>> loadKeywordsFromManifest()
        {
            #region implementation

            // Check cache validity
            if (_skillKeywordsCache != null &&
                DateTime.UtcNow - _skillKeywordsCacheTimestamp < _cacheDuration)
            {
                return _skillKeywordsCache;
            }

            _logger.LogDebug("Loading skill keywords from skillSelector.md manifest");

            var skillKeywords = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Load the manifest file
                var manifestPath = _configuration.GetValue<string>("ClaudeApiSettings:Skill-Selector");
                if (string.IsNullOrEmpty(manifestPath))
                {
                    _logger.LogWarning("Skill-Selector path not configured, using default keywords");
                    return getDefaultSkillKeywords();
                }

                var content = readSkillFileByPath(manifestPath);
                if (content.StartsWith("Skills document not found"))
                {
                    _logger.LogWarning("skillSelector.md not found at {Path}, using default keywords", manifestPath);
                    return getDefaultSkillKeywords();
                }

                // Parse the manifest to extract keywords for each skill
                var lines = content.Split('\n');
                string? currentSkill = null;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    // Check for skill section header (### skillName)
                    if (trimmedLine.StartsWith("### ") && !trimmedLine.Contains(" "))
                    {
                        currentSkill = trimmedLine.Substring(4).Trim();
                        if (!skillKeywords.ContainsKey(currentSkill))
                        {
                            skillKeywords[currentSkill] = new List<string>();
                        }
                    }
                    // Check for keywords line (**Keywords**: ...)
                    else if (currentSkill != null && trimmedLine.StartsWith("**Keywords**:"))
                    {
                        var keywordsText = trimmedLine.Substring("**Keywords**:".Length).Trim();
                        var keywords = keywordsText
                            .Split(',')
                            .Select(k => k.Trim().ToLowerInvariant())
                            .Where(k => !string.IsNullOrWhiteSpace(k))
                            .ToList();

                        skillKeywords[currentSkill].AddRange(keywords);
                    }
                }

                // Validate we got meaningful keywords
                if (skillKeywords.Count == 0 || !skillKeywords.Any(kvp => kvp.Value.Count > 0))
                {
                    _logger.LogWarning("No keywords found in skillSelector.md, using default keywords");
                    return getDefaultSkillKeywords();
                }

                _skillKeywordsCache = skillKeywords;
                _skillKeywordsCacheTimestamp = DateTime.UtcNow;

                _logger.LogInformation("Loaded keywords for {SkillCount} skills from skillSelector.md", skillKeywords.Count);

                return skillKeywords;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading keywords from skillSelector.md, using default keywords");
                return getDefaultSkillKeywords();
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns default skill keywords as a fallback when skillSelector.md cannot be loaded.
        /// </summary>
        /// <returns>Dictionary of default skill keywords.</returns>
        /// <remarks>
        /// This fallback ensures the system continues to function even if the manifest
        /// file is missing or corrupted. The default keywords should be kept in sync
        /// with skillSelector.md when possible.
        /// </remarks>
        private Dictionary<string, List<string>> getDefaultSkillKeywords()
        {
            #region implementation

            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["labelIndicationWorkflow"] = new List<string>
                {
                    "what helps with", "what can help", "i have", "options for",
                    "treatment for", "products for", "what can i take",
                    "alternatives to", "feeling down", "high blood pressure",
                    "depression", "anxiety", "diabetes", "hypertension",
                    "cholesterol", "pain relief", "condition", "symptom",
                    "disease", "therapeutic", "indication", "what product",
                    "which medication", "generics", "same ingredient",
                    "what treats", "medicine for", "drug for",
                    "generic alternative", "similar to", "like lipitor",
                    "like prozac", "equivalent to",
                    // Additional keywords for specific conditions
                    "indicated for", "what is indicated", "shingles", "postherpetic",
                    "neuralgia", "neuropathic", "neuropathy", "seizure", "epilepsy", "nerve pain"
                },
                ["userActivity"] = new List<string>
                {
                    "log", "logs", "error log", "warning log", "application log", "admin log",
                    "debug", "trace", "diagnostic", "log statistics", "log categor", "log level",
                    "show errors", "show warnings", "recent errors", "what errors",
                    "user activity", "activity log", "what did user", "user's activity",
                    "what did", "how many times",
                    "endpoint performance", "endpoint stats", "response time",
                    "controller performance", "api performance", "how fast",
                    "performance for", "performance of"
                },
                ["settings"] = new List<string>
                {
                    "cache", "clear cache", "reset cache", "flush cache", "invalidate cache"
                },
                ["label"] = new List<string>
                {
                    "drug", "product", "ingredient", "ndc", "manufacturer", "labeler",
                    "import", "export", "label", "section", "warning", "side effect",
                    "dosage", "dose", "interaction", "contraindication", "adverse",
                    "prescribing", "pharmaceutical", "medication", "medicine", "tablet",
                    "capsule", "injection", "anda", "nda", "bla", "pharmacologic",
                    "therapeutic", "class", "fda", "spl", "document"
                },
                ["rescueWorkflow"] = new List<string>
                {
                    "not found", "empty results", "where else", "alternative",
                    "rescue", "fallback", "text search", "description section",
                    "inactive ingredient", "excipient", "not available",
                    "couldn't find", "not in", "extract from text"
                },
                ["general"] = new List<string>
                {
                    "conversation", "interpret", "synthesize", "chat",
                    "login", "logout", "authenticate", "sign in", "sign out",
                    "profile", "current user", "who am i", "context",
                    "oauth", "google login", "microsoft login"
                },
                ["equianalgesicConversion"] = new List<string>
                {
                    "equianalgesic", "opioid conversion", "convert morphine",
                    "convert hydromorphone", "convert fentanyl", "convert oxycodone",
                    "convert methadone", "convert buprenorphine",
                    "morphine equivalent", "mme", "opioid tolerant", "dose conversion",
                    "switching opioids", "opioid switch", "equivalent dose",
                    "morphine to hydromorphone", "hydromorphone to morphine",
                    "fentanyl to morphine", "morphine to fentanyl",
                    "oxycodone to morphine", "morphine to oxycodone",
                    "methadone", "buprenorphine",
                    "methadone to buprenorphine", "buprenorphine to methadone",
                    "opioid", "conversion from", "conversion to"
                }
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Reads a skill file from the configured path with caching support.
        /// </summary>
        /// <param name="configKey">The configuration key under ClaudeApiSettings.</param>
        /// <param name="cacheKeyPrefix">A prefix for the cache key.</param>
        /// <returns>The skill file content as a string.</returns>
        private string readSkillFile(string configKey, string cacheKeyPrefix)
        {
            #region implementation

            // Use consistent cache key naming convention
            var key = $"{cacheKeyPrefix}_ClaudeApiSettings_{configKey}".Base64Encode();
            var cachedSkills = PerformanceHelper.GetCache<string>(key);

            if (!string.IsNullOrEmpty(cachedSkills))
            {
                return cachedSkills;
            }

            // Get the configured path from appsettings
            var skillFilePath = _configuration.GetValue<string>($"ClaudeApiSettings:{configKey}");

            if (string.IsNullOrEmpty(skillFilePath))
            {
                return $"{configKey} configuration not found.";
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
