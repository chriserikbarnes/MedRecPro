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

        /**************************************************************/
        /// <summary>
        /// Retrieves the synthesis rules document containing content quality and aggregation rules.
        /// </summary>
        /// <returns>
        /// A task that resolves to the synthesis rules document content as a string.
        /// </returns>
        /// <remarks>
        /// Contains truncation detection, 404 handling, and multi-product aggregation rules.
        /// Only available when using the Refactored pathway.
        /// </remarks>
        /// <seealso cref="GetResponseFormatDocumentAsync"/>
        Task<string> GetSynthesisRulesDocumentAsync();

        /**************************************************************/
        /// <summary>
        /// Gets the currently active skill pathway type.
        /// </summary>
        /// <returns>
        /// A string indicating the active pathway ("Refactored" or "FirstDraft").
        /// </returns>
        /// <remarks>
        /// Useful for diagnostic purposes and conditional UI rendering.
        /// </remarks>
        string GetActiveSkillPathway();

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
        /// Tracks the pathway used for the last manifest cache to detect pathway changes.
        /// </summary>
        private SkillPathwayType? _lastManifestPathway;

        /**************************************************************/
        /// <summary>
        /// Cache duration for skill documents (8 hours).
        /// </summary>
        private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(8);

        /**************************************************************/
        /// <summary>
        /// Defines the available skill pathway options.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><term>Refactored</term><description>Uses the new separated concerns architecture with skills.md, selectors.md, and interfaces/</description></item>
        /// <item><term>FirstDraft</term><description>Uses the original monolithic skill files from Skills/FirstDraft/</description></item>
        /// </list>
        /// </remarks>
        public enum SkillPathwayType
        {
            /// <summary>
            /// Uses the new refactored skill architecture with separated concerns.
            /// </summary>
            Refactored,

            /// <summary>
            /// Uses the original FirstDraft skill files (legacy pathway).
            /// </summary>
            FirstDraft
        }

        /**************************************************************/
        /// <summary>
        /// Gets the currently active skill pathway based on the FeatureFlags.SkillPathway configuration.
        /// </summary>
        /// <remarks>
        /// Reads from FeatureFlags:SkillPathway in appsettings.json.
        /// Defaults to <see cref="SkillPathwayType.Refactored"/> if not configured or invalid.
        /// </remarks>
        /// <seealso cref="SkillPathwayType"/>
        private SkillPathwayType ActivePathway
        {
            get
            {
                var pathwaySetting = _configuration.GetValue<string>("FeatureFlags:SkillPathway");
                if (Enum.TryParse<SkillPathwayType>(pathwaySetting, ignoreCase: true, out var pathway))
                {
                    return pathway;
                }
                return SkillPathwayType.Refactored; // Default to refactored architecture
            }
        }

        /**************************************************************/
        /// <summary>
        /// Gets the configuration section name for skill paths based on the active pathway.
        /// </summary>
        /// <remarks>
        /// Returns "ClaudeApiSettings" for Refactored pathway,
        /// "FirstDraftSkillSettings" for FirstDraft pathway.
        /// </remarks>
        /// <seealso cref="ActivePathway"/>
        private string SkillConfigSection => ActivePathway == SkillPathwayType.FirstDraft
            ? "FirstDraftSkillSettings"
            : "ClaudeApiSettings";

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
        /// Only used when <see cref="ActivePathway"/> is <see cref="SkillPathwayType.Refactored"/>.
        /// </remarks>
        /// <seealso cref="ActivePathway"/>
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

            // Alias mappings for skill names used in keyword selection
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
        /// Path to the synthesis rules document in the refactored architecture.
        /// </summary>
        /// <remarks>
        /// Contains content quality detection, aggregation rules, and 404 handling guidelines.
        /// Only used when <see cref="ActivePathway"/> is <see cref="SkillPathwayType.Refactored"/>.
        /// </remarks>
        private const string SynthesisRulesPath = "Skills/interfaces/synthesis-rules.md";

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
        /// <remarks>
        /// The manifest is loaded based on the active skill pathway.
        /// For FirstDraft pathway, uses Skills/FirstDraft/skillSelector.md.
        /// For Refactored pathway, uses Skills/selectors.md (new architecture).
        /// </remarks>
        /// <seealso cref="ActivePathway"/>
        public async Task<string> GetSkillManifestAsync()
        {
            #region implementation

            // Include pathway in cache key to support pathway switching
            var cacheKey = $"SkillManifest_{ActivePathway}";

            // Check cache validity (include pathway in cache to prevent cross-contamination)
            if (_manifestCache != null &&
                DateTime.UtcNow - _manifestCacheTimestamp < _cacheDuration &&
                _lastManifestPathway == ActivePathway)
            {
                return _manifestCache;
            }

            _logger.LogDebug("Building skill manifest for pathway: {Pathway}", ActivePathway);

            // Load the skill selector manifest from file based on pathway
            var manifestPath = _configuration.GetValue<string>($"{SkillConfigSection}:Skill-Selector");

            // For refactored pathway, prefer the new selectors.md if available
            if (ActivePathway == SkillPathwayType.Refactored)
            {
                var selectorsContent = await GetSelectorsDocumentAsync();
                if (!selectorsContent.StartsWith("Skills document not found"))
                {
                    _manifestCache = selectorsContent;
                    _manifestCacheTimestamp = DateTime.UtcNow;
                    _lastManifestPathway = ActivePathway;
                    return _manifestCache;
                }
            }

            // Load from configured path
            if (!string.IsNullOrEmpty(manifestPath))
            {
                var content = readSkillFileByPath(manifestPath);
                if (!content.StartsWith("Skills document not found") && !content.StartsWith("Skill-Selector"))
                {
                    _manifestCache = content;
                    _manifestCacheTimestamp = DateTime.UtcNow;
                    _lastManifestPathway = ActivePathway;
                    return _manifestCache;
                }
            }

            // Fallback: build manifest from available skills
            _manifestCache = await buildSkillManifestAsync();
            _manifestCacheTimestamp = DateTime.UtcNow;
            _lastManifestPathway = ActivePathway;

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
        /// <remarks>
        /// For the Refactored pathway, this method also loads:
        /// 1. The interface document for each selected skill (contains API endpoints and workflows)
        /// 2. The response-format.md document (contains output requirements)
        /// 3. The synthesis-rules.md document (contains content quality rules)
        /// This ensures Claude has complete instructions for generating proper responses with
        /// label links and data source attribution.
        /// </remarks>
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

                    // For Refactored pathway, also load the interface document
                    if (ActivePathway == SkillPathwayType.Refactored)
                    {
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
            }

            // For Refactored pathway, append response format and synthesis rules
            if (ActivePathway == SkillPathwayType.Refactored)
            {
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
            }

            _logger.LogInformation("[SKILL CONTENT] Loaded skills [{Skills}] with pathway {Pathway}",
                string.Join(", ", selection.SelectedSkills), ActivePathway);

            return sb.ToString();

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        /// <remarks>
        /// For the Refactored pathway, if the skill file is not found, attempts to return
        /// the interface document instead. This allows the new architecture to work where
        /// skill files may not exist in the root skills folder.
        /// </remarks>
        /// <seealso cref="GetInterfaceDocumentAsync"/>
        /// <seealso cref="ActivePathway"/>
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

                // For Refactored pathway, if skill file not found, try to use interface document
                if (content.StartsWith("Skills document not found") && ActivePathway == SkillPathwayType.Refactored)
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

                // For Refactored pathway, if skill file not found, try to use interface document
                if (content.StartsWith("Skills document not found") && ActivePathway == SkillPathwayType.Refactored)
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

        #region legacy compatibility methods

        /**************************************************************/
        /// <inheritdoc/>
        /// <remarks>
        /// Loads skills based on the active pathway. Cache keys include the pathway
        /// to prevent cross-contamination when switching pathways.
        /// </remarks>
        /// <seealso cref="ActivePathway"/>
        public async Task<string> GetFullSkillsDocumentAsync()
        {
            #region implementation

            // Include pathway in cache key to support pathway switching
            var key = $"ClaudeSkillService_GetFullSkillsDocument_{ActivePathway}".Base64Encode();
            var cached = PerformanceHelper.GetCache<string>(key);

            if (!string.IsNullOrEmpty(cached))
            {
                return cached;
            }

            _logger.LogDebug("Building full skills document for pathway: {Pathway}", ActivePathway);

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
        /// <remarks>
        /// The capability contracts document is only available for the Refactored pathway.
        /// For FirstDraft pathway, this returns a message indicating the document is unavailable.
        /// </remarks>
        /// <seealso cref="GetSelectorsDocumentAsync"/>
        /// <seealso cref="GetInterfaceDocumentAsync"/>
        /// <seealso cref="ActivePathway"/>
        public Task<string> GetCapabilityContractsAsync()
        {
            #region implementation

            // Only available for Refactored pathway
            if (ActivePathway != SkillPathwayType.Refactored)
            {
                return Task.FromResult(
                    "Capability contracts document is only available when using the Refactored skill pathway.");
            }

            var key = $"ClaudeSkillService_CapabilityContracts_{ActivePathway}".Base64Encode();
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
        /// For Refactored pathway, returns Skills/selectors.md.
        /// For FirstDraft pathway, returns Skills/FirstDraft/skillSelector.md.
        /// </remarks>
        /// <seealso cref="GetCapabilityContractsAsync"/>
        /// <seealso cref="GetInterfaceDocumentAsync"/>
        /// <seealso cref="ActivePathway"/>
        public Task<string> GetSelectorsDocumentAsync()
        {
            #region implementation

            var key = $"ClaudeSkillService_SelectorsDoc_{ActivePathway}".Base64Encode();
            var cached = PerformanceHelper.GetCache<string>(key);

            if (!string.IsNullOrEmpty(cached))
            {
                return Task.FromResult(cached);
            }

            // Determine which selector document to load based on pathway
            var docPath = ActivePathway == SkillPathwayType.Refactored
                ? SelectorsDocPath
                : _configuration.GetValue<string>($"{SkillConfigSection}:Skill-Selector") ?? "Skills/FirstDraft/skillSelector.md";

            _logger.LogDebug("Loading selectors document from {Path} for pathway {Pathway}", docPath, ActivePathway);

            var content = readSkillFileByPath(docPath);

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
        /// Interface documents are only available for the Refactored pathway.
        /// For FirstDraft pathway, use <see cref="GetSkillByNameAsync"/> instead.
        /// </remarks>
        /// <seealso cref="GetCapabilityContractsAsync"/>
        /// <seealso cref="GetSelectorsDocumentAsync"/>
        /// <seealso cref="ActivePathway"/>
        public Task<string> GetInterfaceDocumentAsync(string skillName)
        {
            #region implementation

            // Only available for Refactored pathway
            if (ActivePathway != SkillPathwayType.Refactored)
            {
                return Task.FromResult(
                    $"Interface documents are only available when using the Refactored skill pathway. " +
                    $"For FirstDraft pathway, use GetSkillByNameAsync('{skillName}') instead.");
            }

            if (string.IsNullOrWhiteSpace(skillName))
            {
                return Task.FromResult("Skill name cannot be empty.");
            }

            var normalizedName = skillName.Trim();

            // Try direct lookup first
            if (_interfaceDocPaths.TryGetValue(normalizedName, out var docPath))
            {
                var key = $"ClaudeSkillService_Interface_{normalizedName}_{ActivePathway}".Base64Encode();
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

            var key = $"ClaudeSkillService_ResponseFormat_{ActivePathway}".Base64Encode();
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

            // Only available for Refactored pathway
            if (ActivePathway != SkillPathwayType.Refactored)
            {
                return Task.FromResult(
                    "Synthesis rules document is only available when using the Refactored skill pathway. " +
                    "For FirstDraft pathway, use GetSynthesisPromptSkills() instead.");
            }

            var key = $"ClaudeSkillService_SynthesisRules".Base64Encode();
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

        /**************************************************************/
        /// <inheritdoc/>
        public string GetActiveSkillPathway()
        {
            return ActivePathway.ToString();
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
            var isEquianalgesicQuery = false;

            // Load keywords from skillSelector.md (cached after first load)
            var skillKeywords = loadKeywordsFromManifest();

            // Check for equianalgesic conversion FIRST - this takes priority over indication workflow
            // Equianalgesic queries should NOT load labelProductIndication (indication reference data)
            if (skillKeywords.TryGetValue("equianalgesicConversion", out var equianalgesicKeywords))
            {
                var matchedKeyword = equianalgesicKeywords.FirstOrDefault(k => message.Contains(k));
                if (matchedKeyword != null)
                {
                    selectedSkills.Add("equianalgesicConversion");
                    isEquianalgesicQuery = true;
                    _logger.LogInformation("[SKILL SELECTION] equianalgesicConversion matched on keyword: '{Keyword}'", matchedKeyword);
                }
            }

            // Check for indication workflow skill (condition-based queries)
            // SKIP if equianalgesic was already selected - they have overlapping keywords like "pain", "opioid"
            // but equianalgesic uses ingredient search, not indication-based discovery
            if (!isEquianalgesicQuery && skillKeywords.TryGetValue("labelIndicationWorkflow", out var indicationWorkflowKeywords))
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
            // Skip for equianalgesic queries - they have their own workflow
            if (!isEquianalgesicQuery && skillKeywords.TryGetValue("label", out var labelKeywords))
            {
                // Define detailed label content keywords inline (these are specific to label sections)
                var needsDetailedLabelContent = new[] { "side effect", "warning", "section", "adverse", "contraindication" };

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
        /// Tracks the pathway used for the last keywords cache to detect pathway changes.
        /// </summary>
        private SkillPathwayType? _lastKeywordsPathway;

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
        /// This method reads the skillSelector.md file based on the active pathway.
        /// For FirstDraft pathway, uses Skills/FirstDraft/skillSelector.md.
        /// For Refactored pathway, uses Skills/selectors.md.
        /// Results are cached for the same duration as other skill content.
        ///
        /// Expected manifest format:
        /// ### skillName
        /// **Keywords**: keyword1, keyword2, keyword phrase, ...
        /// </remarks>
        /// <seealso cref="GetSkillManifestAsync"/>
        /// <seealso cref="ActivePathway"/>
        private Dictionary<string, List<string>> loadKeywordsFromManifest()
        {
            #region implementation

            // Check cache validity (include pathway in validity check)
            if (_skillKeywordsCache != null &&
                DateTime.UtcNow - _skillKeywordsCacheTimestamp < _cacheDuration &&
                _lastKeywordsPathway == ActivePathway)
            {
                return _skillKeywordsCache;
            }

            _logger.LogDebug("Loading skill keywords from manifest for pathway: {Pathway}", ActivePathway);

            var skillKeywords = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Load the manifest file based on active pathway
                var manifestPath = _configuration.GetValue<string>($"{SkillConfigSection}:Skill-Selector");

                // For refactored pathway, try selectors.md first
                if (ActivePathway == SkillPathwayType.Refactored)
                {
                    var selectorsPath = SelectorsDocPath;
                    var selectorsContent = readSkillFileByPath(selectorsPath);
                    if (!selectorsContent.StartsWith("Skills document not found"))
                    {
                        manifestPath = selectorsPath;
                    }
                }

                if (string.IsNullOrEmpty(manifestPath))
                {
                    _logger.LogWarning("Skill-Selector path not configured for {Pathway} pathway, using default keywords", ActivePathway);
                    return getDefaultSkillKeywords();
                }

                var content = readSkillFileByPath(manifestPath);
                if (content.StartsWith("Skills document not found"))
                {
                    _logger.LogWarning("Skill manifest not found at {Path}, using default keywords", manifestPath);
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
                    _logger.LogWarning("No keywords found in skill manifest, using default keywords");
                    return getDefaultSkillKeywords();
                }

                _skillKeywordsCache = skillKeywords;
                _skillKeywordsCacheTimestamp = DateTime.UtcNow;
                _lastKeywordsPathway = ActivePathway;

                _logger.LogInformation("Loaded keywords for {SkillCount} skills from {Pathway} pathway manifest",
                    skillKeywords.Count, ActivePathway);

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
                    // Core label/drug terms
                    "drug", "product", "ingredient", "ndc", "manufacturer", "labeler",
                    "import", "export", "label", "section", "warning", "side effect",
                    "dosage", "dose", "interaction", "contraindication", "adverse",
                    "prescribing", "pharmaceutical", "medication", "medicine", "tablet",
                    "capsule", "injection", "anda", "nda", "bla", "pharmacologic",
                    "therapeutic", "class", "fda", "spl", "document",
                    // General information query patterns
                    "tell me about", "what is", "information about", "details about",
                    "know about", "learn about"
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
                    // Core conversion terms
                    "equianalgesic", "opioid conversion", "morphine equivalent", "mme",
                    "dose conversion", "switching opioids", "opioid switch", "equivalent dose",
                    // Conversion action phrases
                    "convert from", "convert to", "conversion from", "conversion to",
                    "convert morphine", "convert hydromorphone", "convert fentanyl",
                    "convert oxycodone", "convert methadone", "convert buprenorphine",
                    // Drug-to-drug conversion patterns (X to Y)
                    "morphine to hydromorphone", "hydromorphone to morphine",
                    "fentanyl to morphine", "morphine to fentanyl",
                    "oxycodone to morphine", "morphine to oxycodone",
                    "oxycodone to buprenorphine", "buprenorphine to oxycodone",
                    "oxycodone to hydromorphone", "hydromorphone to oxycodone",
                    "oxycodone to fentanyl", "fentanyl to oxycodone",
                    "methadone to buprenorphine", "buprenorphine to methadone",
                    "methadone to morphine", "morphine to methadone",
                    "hydromorphone to buprenorphine", "buprenorphine to hydromorphone",
                    "fentanyl to buprenorphine", "buprenorphine to fentanyl",
                    // NOTE: Do NOT include standalone drug names like "buprenorphine", "methadone", "opioid"
                    // as these would incorrectly route general drug info queries to the conversion path
                }
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Reads a skill file from the configured path with caching support.
        /// Uses the active skill pathway to determine which configuration section to read from.
        /// </summary>
        /// <param name="configKey">The configuration key (e.g., "Skill-Label", "Skill-Settings").</param>
        /// <param name="cacheKeyPrefix">A prefix for the cache key.</param>
        /// <returns>The skill file content as a string.</returns>
        /// <remarks>
        /// The method reads from either ClaudeApiSettings or FirstDraftSkillSettings
        /// based on the <see cref="ActivePathway"/> property. Cache keys include
        /// the pathway to prevent cross-contamination between pathways.
        /// </remarks>
        /// <seealso cref="ActivePathway"/>
        /// <seealso cref="SkillConfigSection"/>
        private string readSkillFile(string configKey, string cacheKeyPrefix)
        {
            #region implementation

            // Include pathway in cache key to prevent cross-contamination
            var key = $"{cacheKeyPrefix}_{SkillConfigSection}_{configKey}".Base64Encode();
            var cachedSkills = PerformanceHelper.GetCache<string>(key);

            if (!string.IsNullOrEmpty(cachedSkills))
            {
                return cachedSkills;
            }

            // Get the configured path from the active pathway's settings section
            var skillFilePath = _configuration.GetValue<string>($"{SkillConfigSection}:{configKey}");

            // Fallback to ClaudeApiSettings if FirstDraft doesn't have the key
            if (string.IsNullOrEmpty(skillFilePath) && ActivePathway == SkillPathwayType.FirstDraft)
            {
                skillFilePath = _configuration.GetValue<string>($"ClaudeApiSettings:{configKey}");
                _logger.LogDebug("Falling back to ClaudeApiSettings for {ConfigKey}", configKey);
            }

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
