using MedRecProImportClass.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace MedRecProImportClass.Service.TransformationServices
{
    #region interface

    /**************************************************************/
    /// <summary>
    /// Defines the contract for Stage 3.5 AI-powered correction of parsed table observations.
    /// After Stage 3 parsers produce <see cref="ParsedObservation"/> objects, this service
    /// sends them to Claude for semantic review and correction of misclassified fields
    /// before they are written to the database.
    /// </summary>
    /// <remarks>
    /// The correction service is optional — if not registered in DI, the orchestrator
    /// skips correction and writes parser output directly. When enabled, it:
    /// - Groups observations by TextTableID for contextual review
    /// - Sends compact JSON payloads to Claude Haiku for fast, low-cost correction
    /// - Applies returned corrections in-memory and appends AI_CORRECTED flags
    /// - Fails gracefully on API errors (returns original observations unchanged)
    ///
    /// ## Skill Reference
    /// The system prompt encodes rules from the table-parser-data-dictionary skill:
    /// column-contracts.md, normalization-rules.md, and table-types.md.
    /// Claude corrects: PrimaryValueType migration, DoseRegimen triage, Unit scrub,
    /// ParameterName/TreatmentArm cleanup, ParameterCategory SOC normalization,
    /// and BoundType inference.
    /// </remarks>
    /// <seealso cref="ClaudeApiCorrectionService"/>
    /// <seealso cref="ClaudeApiCorrectionSettings"/>
    /// <seealso cref="TableParsingOrchestrator"/>
    public interface IClaudeApiCorrectionService
    {
        /**************************************************************/
        /// <summary>
        /// Reviews a batch of parsed observations and applies AI-suggested corrections
        /// to misclassified fields. Observations are grouped by TextTableID and sent
        /// to Claude in table-level batches for contextual accuracy.
        /// </summary>
        /// <param name="observations">Parsed observations from Stage 3 parsers.</param>
        /// <param name="originalTables">Optional lookup of original reconstructed tables keyed by TextTableID for comparison context.</param>
        /// <param name="progress">Optional progress callback reporting 0–100 within the correction stage.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The corrected observations (same list, mutated in place).</returns>
        Task<List<ParsedObservation>> CorrectBatchAsync(
            List<ParsedObservation> observations,
            IReadOnlyDictionary<int, ReconstructedTable>? originalTables = null,
            IProgress<TransformBatchProgress>? progress = null,
            CancellationToken ct = default);
    }

    #endregion

    #region implementation

    /**************************************************************/
    /// <summary>
    /// Claude API client that performs post-parse correction of <see cref="ParsedObservation"/>
    /// objects by sending them to Claude for semantic review. Identifies and corrects common
    /// parser misclassifications across all TableCategory types using the full normalization
    /// rule set from the table-parser-data-dictionary skill.
    /// </summary>
    /// <remarks>
    /// ## Integration Point
    /// Called by <see cref="TableParsingOrchestrator"/> after <c>parser.Parse(table)</c>
    /// but before <c>mapToEntity</c> + <c>SaveChangesAsync</c>.
    ///
    /// ## API Pattern
    /// Uses direct HTTP POST to Anthropic Messages API (same pattern as MedRecPro's
    /// ClaudeApiService) with Newtonsoft.Json for serialization.
    ///
    /// ## Batching Strategy
    /// 1. Group observations by TextTableID (table-level context)
    /// 2. Split groups exceeding MaxObservationsPerRequest into sub-batches
    /// 3. Send each sub-batch as a separate API request with configurable delay
    ///
    /// ## Failure Handling
    /// All API failures are non-fatal: the service logs a warning and returns
    /// original observations unchanged. The pipeline never fails due to AI correction.
    ///
    /// ## Correctable Fields
    /// ParameterName, PrimaryValueType, SecondaryValueType, TreatmentArm, DoseRegimen,
    /// Population, Unit, ParameterCategory, ParameterSubtype, Timepoint, TimeUnit,
    /// StudyContext, BoundType.
    ///
    /// ## Skill Reference
    /// System prompt encodes rules from column-contracts.md, normalization-rules.md,
    /// and table-types.md. See the table-parser-data-dictionary skill for full context.
    /// </remarks>
    /// <seealso cref="IClaudeApiCorrectionService"/>
    /// <seealso cref="ClaudeApiCorrectionSettings"/>
    public class ClaudeApiCorrectionService : IClaudeApiCorrectionService
    {
        #region Fields

        /**************************************************************/
        /// <summary>HttpClient for Anthropic API calls.</summary>
        private readonly HttpClient _httpClient;

        /**************************************************************/
        /// <summary>Configuration settings.</summary>
        private readonly ClaudeApiCorrectionSettings _settings;

        /**************************************************************/
        /// <summary>Logger for diagnostics.</summary>
        private readonly ILogger<ClaudeApiCorrectionService> _logger;

        /**************************************************************/
        /// <summary>
        /// Set of field names that the AI is allowed to correct.
        /// Corrections targeting other fields are silently ignored.
        /// Derived from the correctable-field list in the table-parser-data-dictionary skill.
        /// </summary>
        private static readonly HashSet<string> CorrectableFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "ParameterName", "PrimaryValueType", "SecondaryValueType",
            "TreatmentArm", "DoseRegimen", "Population", "Unit",
            "ParameterCategory", "ParameterSubtype", "Timepoint", "TimeUnit",
            "StudyContext", "BoundType"
        };

        /**************************************************************/
        /// <summary>
        /// Cached system prompt loaded from skill file (lazy initialized on first API call).
        /// Falls back to a minimal default if the skill file is missing or empty.
        /// </summary>
        private string? _cachedSystemPrompt;

        /**************************************************************/
        /// <summary>
        /// Cached pivot comparison instructions loaded from skill file (lazy initialized).
        /// </summary>
        private string? _cachedPivotComparisonPrompt;

        /**************************************************************/
        /// <summary>Whether skill files have been loaded.</summary>
        private bool _skillFilesLoaded;

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes the correction service with HTTP client and configuration.
        /// </summary>
        /// <param name="httpClient">Pre-configured HttpClient with API key and base address.</param>
        /// <param name="settings">Correction service configuration.</param>
        /// <param name="logger">Logger.</param>
        public ClaudeApiCorrectionService(
            HttpClient httpClient,
            IOptions<ClaudeApiCorrectionSettings> settings,
            ILogger<ClaudeApiCorrectionService> logger)
        {
            #region implementation

            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;

            #endregion
        }

        #endregion

        #region IClaudeApiCorrectionService Implementation

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<List<ParsedObservation>> CorrectBatchAsync(
            List<ParsedObservation> observations,
            IReadOnlyDictionary<int, ReconstructedTable>? originalTables = null,
            IProgress<TransformBatchProgress>? progress = null,
            CancellationToken ct = default)
        {
            #region implementation

            if (observations.Count == 0)
                return observations;

            if (!_settings.Enabled)
            {
                _logger.LogDebug("Claude correction disabled — passing {Count} observations through", observations.Count);
                return observations;
            }

            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                _logger.LogWarning("Claude correction enabled but API key is empty — skipping correction");
                return observations;
            }

            // Gate by ML anomaly score when threshold is configured
            var toCorrect = _settings.MlAnomalyScoreThreshold > 0f
                ? observations.Where(exceedsAnomalyThreshold).ToList()
                : observations;

            if (toCorrect.Count == 0)
            {
                _logger.LogDebug("All {Count} observations below ML anomaly threshold {Threshold} — skipping Claude correction",
                    observations.Count, _settings.MlAnomalyScoreThreshold);
                return observations;
            }

            if (toCorrect.Count < observations.Count)
            {
                _logger.LogInformation("ML gate: {Passed}/{Total} observations exceed anomaly threshold {Threshold} — sending to Claude",
                    toCorrect.Count, observations.Count, _settings.MlAnomalyScoreThreshold);
            }

            // Group by TextTableID for contextual correction
            var groups = toCorrect.GroupBy(o => o.TextTableID ?? 0).ToList();
            var totalCorrections = 0;

            // Count total chunks across all groups for progress reporting
            var totalChunks = groups.Sum(g =>
                (int)Math.Ceiling((double)g.Count() / _settings.MaxObservationsPerRequest));
            var chunksCompleted = 0;

            foreach (var group in groups)
            {
                ct.ThrowIfCancellationRequested();

                var tableObservations = group.ToList();

                // Look up the original table for this group's TextTableID
                ReconstructedTable? groupTable = null;
                originalTables?.TryGetValue((int)group.Key, out groupTable);

                // Split into sub-batches if needed
                var chunks = chunkList(tableObservations, _settings.MaxObservationsPerRequest);

                foreach (var chunk in chunks)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        // Snapshot pre-correction flags for per-observation provenance
                        var preCorrectionFlags = chunk.Select(o => o.ValidationFlags).ToList();

                        var corrections = await requestCorrectionsAsync(chunk, groupTable, ct);
                        var applied = applyCorrections(chunk, corrections);
                        totalCorrections += applied;

                        // Append per-observation Claude confidence provenance flags
                        for (int i = 0; i < chunk.Count; i++)
                        {
                            var obs = chunk[i];
                            var perObsCorrectionCount = countNewAiCorrections(preCorrectionFlags[i], obs.ValidationFlags);
                            appendFlag(obs, $"CONFIDENCE:AI:{obs.ParseConfidence ?? 0:F2}:{perObsCorrectionCount}_corrections");
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Claude correction failed for TextTableID={TableId}, chunk of {Count} observations — using original values",
                            group.Key, chunk.Count);
                    }

                    // Rate limiting delay between requests
                    if (_settings.DelayBetweenRequestsMs > 0)
                    {
                        await Task.Delay(_settings.DelayBetweenRequestsMs, ct);
                    }

                    // Report per-chunk progress (0–100 within the correction stage)
                    chunksCompleted++;
                    progress?.Report(new TransformBatchProgress
                    {
                        CurrentOperation = $"Claude AI correction ({chunksCompleted}/{totalChunks})...",
                        IntraBatchPercent = totalChunks > 0
                            ? (double)chunksCompleted / totalChunks * 100.0
                            : 100.0
                    });
                }
            }

            if (totalCorrections > 0)
            {
                _logger.LogInformation("Claude correction applied {Count} corrections across {Tables} tables",
                    totalCorrections, groups.Count);
            }

            return observations;

            #endregion
        }

        #endregion

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Sends a chunk of observations to Claude and returns parsed corrections.
        /// </summary>
        /// <param name="observations">Observations to review.</param>
        /// <param name="originalTable">Optional original reconstructed table for comparison context.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of correction objects from the API response.</returns>
        private async Task<List<CorrectionEntry>> requestCorrectionsAsync(
            List<ParsedObservation> observations,
            ReconstructedTable? originalTable,
            CancellationToken ct)
        {
            #region implementation

            ensureSkillFilesLoaded();

            var payload = buildCompactPayload(observations);

            // Build structured context header so Claude can apply the right per-category
            // rules without reading each observation's TableCategory field individually.
            var firstObs = observations.FirstOrDefault();
            var contextHeader = new StringBuilder();
            contextHeader.AppendLine($"TableCategory: {firstObs?.TableCategory ?? "UNKNOWN"}");
            contextHeader.AppendLine($"ParentSectionCode: {firstObs?.ParentSectionCode ?? "(none)"}");
            contextHeader.AppendLine($"Caption: {firstObs?.Caption ?? "(none)"}");
            contextHeader.AppendLine($"ObservationCount: {observations.Count}");

            // Append original table context if available
            if (originalTable != null && !string.IsNullOrWhiteSpace(_cachedPivotComparisonPrompt))
            {
                var tableText = renderOriginalTable(originalTable);
                if (!string.IsNullOrWhiteSpace(tableText))
                {
                    contextHeader.AppendLine();
                    contextHeader.AppendLine(_cachedPivotComparisonPrompt);
                    contextHeader.AppendLine(tableText);
                }
            }

            var requestBody = new
            {
                model = _settings.Model,
                max_tokens = _settings.MaxTokens,
                temperature = _settings.Temperature,
                system = _cachedSystemPrompt,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = $"{contextHeader}\nParsed observations:\n{payload}"
                    }
                }
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("v1/messages", content, ct);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var apiResponse = JObject.Parse(responseBody);

            // Extract text content from Claude response
            var textContent = apiResponse["content"]?
                .FirstOrDefault(c => c["type"]?.ToString() == "text")?["text"]?.ToString();

            if (string.IsNullOrWhiteSpace(textContent))
                return new List<CorrectionEntry>();

            // Strip markdown fences if present
            textContent = stripMarkdownFences(textContent);

            // Check if response was truncated (stop_reason != "end_turn")
            var stopReason = apiResponse["stop_reason"]?.ToString();

            return deserializeCorrections(textContent, stopReason == "max_tokens");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Deserializes the corrections JSON, handling truncated responses by
        /// salvaging complete objects from partial JSON arrays.
        /// </summary>
        /// <param name="json">Raw JSON text from Claude.</param>
        /// <param name="wasTruncated">Whether the response hit the max_tokens limit.</param>
        /// <returns>List of successfully parsed corrections.</returns>
        private List<CorrectionEntry> deserializeCorrections(string json, bool wasTruncated)
        {
            #region implementation

            // Sanitize bare NaN/Infinity tokens that Claude sometimes emits as unquoted values.
            // Newtonsoft.Json chokes on bare NaN when the target property is string?.
            var sanitized = sanitizeJsonFloatLiterals(json);

            try
            {
                return JsonConvert.DeserializeObject<List<CorrectionEntry>>(sanitized)
                    ?? new List<CorrectionEntry>();
            }
            catch (JsonException) when (wasTruncated)
            {
                // Response was truncated — try to salvage complete objects
                return salvageTruncatedJson(sanitized);
            }

            // If not truncated but still invalid JSON, let the exception propagate
            // to be caught by the caller's catch block

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Attempts to recover valid correction entries from a truncated JSON array
        /// by finding the last complete object boundary and parsing up to that point.
        /// </summary>
        /// <param name="truncatedJson">Truncated JSON string.</param>
        /// <returns>List of corrections that could be recovered, may be empty.</returns>
        private List<CorrectionEntry> salvageTruncatedJson(string truncatedJson)
        {
            #region implementation

            // Find the last complete object: look for "}," or "}" followed by nothing useful
            var lastCompleteObject = truncatedJson.LastIndexOf("},");
            if (lastCompleteObject < 0)
            {
                _logger.LogDebug("Cannot salvage truncated JSON — no complete objects found");
                return new List<CorrectionEntry>();
            }

            // Take everything up to and including the last complete "}", then close the array
            var salvaged = truncatedJson.Substring(0, lastCompleteObject + 1) + "]";

            try
            {
                var result = JsonConvert.DeserializeObject<List<CorrectionEntry>>(salvaged)
                    ?? new List<CorrectionEntry>();
                _logger.LogDebug("Salvaged {Count} corrections from truncated response", result.Count);
                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "Failed to salvage truncated JSON");
                return new List<CorrectionEntry>();
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a compact JSON payload containing only correction-relevant fields
        /// to minimize token usage. Includes bounds, timepoint, and study context
        /// fields so Claude can apply BoundType inference and timepoint triage rules.
        /// </summary>
        /// <param name="observations">Observations to serialize.</param>
        /// <returns>JSON string of trimmed observation data.</returns>
        private static string buildCompactPayload(List<ParsedObservation> observations)
        {
            #region implementation

            // Include all fields the system prompt references for correction decisions.
            // Omit large provenance fields (DocumentGUID, LabelerName, etc.) to save tokens.
            var compact = observations.Select(o => new
            {
                o.SourceRowSeq,
                o.SourceCellSeq,
                o.ParameterName,
                o.ParameterCategory,
                o.ParameterSubtype,
                o.TreatmentArm,
                o.ArmN,
                o.StudyContext,
                o.DoseRegimen,
                o.Population,
                o.Timepoint,
                o.TimeUnit,
                o.RawValue,
                o.PrimaryValue,
                o.PrimaryValueType,
                o.SecondaryValue,
                o.SecondaryValueType,
                o.LowerBound,
                o.UpperBound,
                o.BoundType,
                o.Unit,
                o.TableCategory,
                o.ParseConfidence,
                o.ParseRule,
                o.Caption
            });

            return JsonConvert.SerializeObject(compact, Formatting.None);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies corrections to in-memory observations. Returns count of corrections applied.
        /// </summary>
        /// <param name="observations">Observations to correct.</param>
        /// <param name="corrections">Corrections from Claude.</param>
        /// <returns>Number of corrections applied.</returns>
        private int applyCorrections(List<ParsedObservation> observations, List<CorrectionEntry> corrections)
        {
            #region implementation

            var applied = 0;

            foreach (var correction in corrections)
            {
                // Validate field name
                if (!CorrectableFields.Contains(correction.Field ?? ""))
                {
                    _logger.LogDebug("Ignoring correction for non-correctable field: {Field}", correction.Field);
                    continue;
                }

                // Find matching observation
                var target = observations.FirstOrDefault(o =>
                    o.SourceRowSeq == correction.SourceRowSeq &&
                    o.SourceCellSeq == correction.SourceCellSeq);

                if (target == null)
                {
                    _logger.LogDebug(
                        "Ignoring correction for non-existent row: SourceRowSeq={Row}, SourceCellSeq={Cell}",
                        correction.SourceRowSeq, correction.SourceCellSeq);
                    continue;
                }

                // Apply the correction via reflection-free property setter
                if (setFieldValue(target, correction.Field!, correction.NewValue))
                {
                    // Append audit flag
                    var flag = $"AI_CORRECTED:{correction.Field}";
                    target.ValidationFlags = string.IsNullOrEmpty(target.ValidationFlags)
                        ? flag
                        : $"{target.ValidationFlags};{flag}";

                    applied++;

                    _logger.LogDebug(
                        "Corrected {Field}: '{Old}' → '{New}' (Row={Row}, Cell={Cell}, Reason={Reason})",
                        correction.Field, correction.OldValue, correction.NewValue,
                        correction.SourceRowSeq, correction.SourceCellSeq, correction.Reason);
                }
            }

            return applied;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Sets a string field on a ParsedObservation by field name.
        /// Returns true if the field was set, false if the field name is unrecognized.
        /// All fields listed in <see cref="CorrectableFields"/> must have a case here.
        /// </summary>
        /// <param name="obs">Target observation.</param>
        /// <param name="fieldName">Field to set (case-insensitive).</param>
        /// <param name="value">New value (null clears the field).</param>
        /// <returns>True if set successfully.</returns>
        /// <seealso cref="CorrectableFields"/>
        private static bool setFieldValue(ParsedObservation obs, string fieldName, string? value)
        {
            #region implementation

            switch (fieldName.ToLowerInvariant())
            {
                case "parametername":
                    obs.ParameterName = value;
                    return true;
                case "primaryvaluetype":
                    obs.PrimaryValueType = value;
                    return true;
                case "secondaryvaluetype":
                    obs.SecondaryValueType = value;
                    return true;
                case "treatmentarm":
                    obs.TreatmentArm = value;
                    return true;
                case "doseregimen":
                    obs.DoseRegimen = value;
                    return true;
                case "population":
                    obs.Population = value;
                    return true;
                case "unit":
                    obs.Unit = value;
                    return true;
                case "parametercategory":
                    obs.ParameterCategory = value;
                    return true;
                case "parametersubtype":
                    obs.ParameterSubtype = value;
                    return true;
                case "timepoint":
                    obs.Timepoint = value;
                    return true;
                case "timeunit":
                    obs.TimeUnit = value;
                    return true;
                case "studycontext":
                    obs.StudyContext = value;
                    return true;
                case "boundtype":
                    obs.BoundType = value;
                    return true;
                default:
                    return false;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether an observation's ML anomaly score exceeds the configured threshold.
        /// Returns true (conservative — send to Claude) when: the score is absent, the value is
        /// "NOMODEL", the value is "ERROR", or the parsed score ≥ threshold.
        /// Returns false only when a valid numeric score is below the threshold.
        /// </summary>
        /// <param name="obs">Observation to evaluate.</param>
        /// <returns>True if the observation should be sent to Claude.</returns>
        private bool exceedsAnomalyThreshold(ParsedObservation obs)
        {
            #region implementation

            if (string.IsNullOrEmpty(obs.ValidationFlags))
                return true; // No flags at all → conservative: send to Claude

            // Find the MLNET_ANOMALY_SCORE token in ValidationFlags
            const string prefix = "MLNET_ANOMALY_SCORE:";
            var startIdx = obs.ValidationFlags.IndexOf(prefix, StringComparison.Ordinal);
            if (startIdx < 0)
                return true; // No anomaly score flag → conservative: send to Claude

            var valueStart = startIdx + prefix.Length;
            var valueEnd = obs.ValidationFlags.IndexOf(';', valueStart);
            var scoreStr = valueEnd >= 0
                ? obs.ValidationFlags.Substring(valueStart, valueEnd - valueStart).Trim()
                : obs.ValidationFlags.Substring(valueStart).Trim();

            // NOMODEL or ERROR → conservative: send to Claude
            if (string.Equals(scoreStr, "NOMODEL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scoreStr, "ERROR", StringComparison.OrdinalIgnoreCase))
                return true;

            // Parse the numeric score
            if (float.TryParse(scoreStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var score))
            {
                return score >= _settings.MlAnomalyScoreThreshold;
            }

            // Unparseable → conservative: send to Claude
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Strips markdown code fences from API response text.
        /// Claude sometimes wraps JSON in ```json ... ``` despite instructions.
        /// </summary>
        /// <param name="text">Raw response text.</param>
        /// <returns>Text with markdown fences removed.</returns>
        private static string stripMarkdownFences(string text)
        {
            #region implementation

            var trimmed = text.Trim();

            if (trimmed.StartsWith("```"))
            {
                // Remove opening fence (with optional language tag)
                var firstNewline = trimmed.IndexOf('\n');
                if (firstNewline >= 0)
                    trimmed = trimmed.Substring(firstNewline + 1);
            }

            if (trimmed.EndsWith("```"))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 3);
            }

            return trimmed.Trim();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Sanitizes bare floating-point literals (<c>NaN</c>, <c>Infinity</c>, <c>-Infinity</c>)
        /// in JSON by quoting them as strings. Claude occasionally emits these as unquoted values
        /// in correction entries (e.g., <c>"newValue": NaN</c>), which Newtonsoft.Json cannot parse
        /// when the target property is <c>string?</c>.
        /// </summary>
        /// <param name="json">Raw JSON text from Claude.</param>
        /// <returns>Sanitized JSON with bare float literals quoted.</returns>
        private static string sanitizeJsonFloatLiterals(string json)
        {
            #region implementation

            // Replace bare NaN, Infinity, -Infinity with quoted string equivalents.
            // Use word-boundary-aware replacements to avoid matching inside quoted strings
            // that already contain these as proper string values.
            return System.Text.RegularExpressions.Regex.Replace(
                json,
                @"(?<=:\s*)(-?(?:NaN|Infinity))(?=\s*[,}\]])",
                "\"$1\"");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Splits a list into chunks of the specified maximum size.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">Source list.</param>
        /// <param name="chunkSize">Maximum chunk size.</param>
        /// <returns>Enumerable of chunks.</returns>
        private static IEnumerable<List<T>> chunkList<T>(List<T> source, int chunkSize)
        {
            #region implementation

            for (int i = 0; i < source.Count; i += chunkSize)
            {
                yield return source.GetRange(i, Math.Min(chunkSize, source.Count - i));
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Loads skill files on first use (lazy initialization). Reads the correction system
        /// prompt and pivot comparison instructions from disk, stripping YAML frontmatter.
        /// Falls back to a minimal default prompt if the skill file is missing or empty.
        /// </summary>
        private void ensureSkillFilesLoaded()
        {
            #region implementation

            if (_skillFilesLoaded)
                return;

            _cachedSystemPrompt = loadSkillFile(_settings.SkillFilePath);
            _cachedPivotComparisonPrompt = loadSkillFile(_settings.PivotComparisonSkillPath);

            if (string.IsNullOrWhiteSpace(_cachedSystemPrompt))
            {
                _logger.LogWarning(
                    "Correction system prompt skill file not found or empty at '{Path}' — using minimal fallback",
                    _settings.SkillFilePath);
                _cachedSystemPrompt = "You review parsed pharmaceutical SPL label table observations for CLEAR errors only. "
                    + "Return ONLY a JSON array of corrections. If nothing is clearly wrong, return [].";
            }
            else
            {
                _logger.LogDebug("Loaded correction system prompt from '{Path}' ({Length} chars)",
                    _settings.SkillFilePath, _cachedSystemPrompt.Length);
            }

            if (!string.IsNullOrWhiteSpace(_cachedPivotComparisonPrompt))
            {
                _logger.LogDebug("Loaded pivot comparison prompt from '{Path}' ({Length} chars)",
                    _settings.PivotComparisonSkillPath, _cachedPivotComparisonPrompt.Length);
            }

            _skillFilesLoaded = true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Loads a skill file from disk, stripping YAML frontmatter delimited by <c>---</c> lines.
        /// Resolves relative paths from the application base directory.
        /// </summary>
        /// <param name="relativePath">Relative path to the skill file.</param>
        /// <returns>Skill file body content, or null if the file is missing or empty.</returns>
        private static string? loadSkillFile(string? relativePath)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(relativePath))
                return null;

            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var fullPath = Path.Combine(basePath, relativePath);

            if (!File.Exists(fullPath))
                return null;

            var content = File.ReadAllText(fullPath);
            if (string.IsNullOrWhiteSpace(content))
                return null;

            // Strip YAML frontmatter (--- delimited block at start of file)
            return stripYamlFrontmatter(content);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Strips YAML frontmatter from a skill file. Frontmatter is a block delimited
        /// by <c>---</c> lines at the very start of the file.
        /// </summary>
        /// <param name="content">Raw file content.</param>
        /// <returns>Content with frontmatter removed.</returns>
        private static string stripYamlFrontmatter(string content)
        {
            #region implementation

            var trimmed = content.TrimStart();
            if (!trimmed.StartsWith("---"))
                return content;

            // Find the closing --- delimiter
            var closingIdx = trimmed.IndexOf("---", 3);
            if (closingIdx < 0)
                return content; // No closing delimiter — return as-is

            // Skip past the closing --- and any trailing newline
            var bodyStart = closingIdx + 3;
            if (bodyStart < trimmed.Length && trimmed[bodyStart] == '\r')
                bodyStart++;
            if (bodyStart < trimmed.Length && trimmed[bodyStart] == '\n')
                bodyStart++;

            return trimmed.Substring(bodyStart).Trim();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Renders a <see cref="ReconstructedTable"/> as compact pipe-delimited text for
        /// inclusion in the Claude API user message. Includes caption, header columns,
        /// and up to <paramref name="maxRows"/> body rows.
        /// </summary>
        /// <param name="table">The original reconstructed table from Stage 2.</param>
        /// <param name="maxRows">Maximum number of body rows to include (default: 20).</param>
        /// <returns>Pipe-delimited text table, or null if the table has no renderable content.</returns>
        private static string? renderOriginalTable(ReconstructedTable table, int maxRows = 20)
        {
            #region implementation

            var sb = new StringBuilder();

            // Caption
            if (!string.IsNullOrWhiteSpace(table.Caption))
            {
                sb.AppendLine($"Caption: {table.Caption}");
            }

            // Determine column count
            var colCount = table.TotalColumnCount ?? 0;
            if (colCount == 0 && table.Rows?.Count > 0)
            {
                colCount = table.Rows.Max(r => r.Cells?.Count ?? 0);
            }

            if (colCount == 0)
                return null;

            // Render header rows
            var headerRows = table.Rows?.Where(r =>
                string.Equals(r.RowGroupType, "Header", StringComparison.OrdinalIgnoreCase)).ToList();

            if (headerRows?.Count > 0)
            {
                foreach (var row in headerRows)
                {
                    var cells = row.Cells?.Select(c => c.CleanedText ?? c.RawCellText ?? "").ToList()
                        ?? new List<string>();
                    sb.AppendLine("| " + string.Join(" | ", cells) + " |");
                }
                // Separator line
                sb.AppendLine("| " + string.Join(" | ", Enumerable.Repeat("---", colCount)) + " |");
            }

            // Render body rows (up to maxRows)
            var bodyRows = table.Rows?.Where(r =>
                string.Equals(r.RowGroupType, "Body", StringComparison.OrdinalIgnoreCase))
                .Take(maxRows).ToList();

            if (bodyRows?.Count > 0)
            {
                foreach (var row in bodyRows)
                {
                    var cells = row.Cells?.Select(c => c.CleanedText ?? c.RawCellText ?? "").ToList()
                        ?? new List<string>();
                    sb.AppendLine("| " + string.Join(" | ", cells) + " |");
                }

                var totalBodyRows = table.Rows?.Count(r =>
                    string.Equals(r.RowGroupType, "Body", StringComparison.OrdinalIgnoreCase)) ?? 0;
                if (totalBodyRows > maxRows)
                {
                    sb.AppendLine($"... ({totalBodyRows - maxRows} more rows omitted)");
                }
            }

            var result = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? null : result;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Appends a flag to the observation's ValidationFlags, separated by "; ".
        /// </summary>
        /// <param name="obs">Observation to flag.</param>
        /// <param name="flag">Flag to append.</param>
        private static void appendFlag(ParsedObservation obs, string flag)
        {
            #region implementation

            obs.ValidationFlags = string.IsNullOrEmpty(obs.ValidationFlags)
                ? flag
                : $"{obs.ValidationFlags}; {flag}";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Counts the number of AI_CORRECTED flags added between pre and post correction states.
        /// </summary>
        /// <param name="preFlags">ValidationFlags before corrections.</param>
        /// <param name="postFlags">ValidationFlags after corrections.</param>
        /// <returns>Number of new AI_CORRECTED flags.</returns>
        private static int countNewAiCorrections(string? preFlags, string? postFlags)
        {
            #region implementation

            if (postFlags == null)
                return 0;

            var newPart = preFlags != null ? postFlags.Substring(preFlags.Length) : postFlags;
            return newPart.Split("AI_CORRECTED").Length - 1;

            #endregion
        }

        #endregion
    }

    #endregion
}
