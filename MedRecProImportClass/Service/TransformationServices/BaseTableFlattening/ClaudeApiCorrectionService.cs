using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

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
    /// Dose, DoseUnit, Population, Unit, ParameterCategory, ParameterSubtype, Timepoint,
    /// TimeUnit, StudyContext, BoundType.
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
        /// <summary>Shared classifier for placebo, sham, vehicle, and zero-dose arm semantics.</summary>
        private readonly IPlaceboArmClassifier _placeboArmClassifier;

        /**************************************************************/
        /// <summary>
        /// Set of field names that the AI is allowed to correct.
        /// Corrections targeting other fields are silently ignored.
        /// Derived from the correctable-field list in the table-parser-data-dictionary skill.
        /// </summary>
        private static readonly HashSet<string> CorrectableFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "ParameterName", "PrimaryValueType", "SecondaryValueType",
            "TreatmentArm", "DoseRegimen", "Dose", "DoseUnit", "Population", "Subpopulation", "Unit",
            "ParameterCategory", "ParameterSubtype", "Timepoint", "TimeUnit",
            "StudyContext", "BoundType"
        };

        /**************************************************************/
        /// <summary>
        /// Regex used to collapse whitespace for exact token comparisons.
        /// </summary>
        private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Regex used for conservative clinical-token comparisons.
        /// </summary>
        private static readonly Regex WordTokenPattern = new(@"[A-Za-z0-9]+", RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Regex used to recognize simple numeric source cells under percent headers.
        /// </summary>
        private static readonly Regex SimpleNumericCellPattern = new(
            @"^\s*(?:[<>]=?\s*)?\d+(?:\.\d+)?\s*%?\s*$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// MedDRA SOC and body-system labels that must not be written into TreatmentArm.
        /// </summary>
        private static readonly HashSet<string> BodySystemTreatmentArmLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            "Blood and Lymphatic System Disorders", "Cardiac Disorders", "Congenital, Familial and Genetic Disorders",
            "Ear and Labyrinth Disorders", "Endocrine Disorders", "Eye Disorders", "Gastrointestinal Disorders",
            "General Disorders", "General Disorders and Administration Site Conditions", "Hepatobiliary Disorders",
            "Immune System Disorders", "Infections and Infestations", "Injury, Poisoning and Procedural Complications",
            "Investigations", "Metabolism and Nutrition Disorders", "Musculoskeletal and Connective Tissue Disorders",
            "Neoplasms Benign, Malignant and Unspecified", "Nervous System Disorders", "Pregnancy, Puerperium and Perinatal Conditions",
            "Psychiatric Disorders", "Renal and Urinary Disorders", "Reproductive System and Breast Disorders",
            "Respiratory, Thoracic and Mediastinal Disorders", "Skin and Subcutaneous Tissue Disorders",
            "Social Circumstances", "Surgical and Medical Procedures", "Vascular Disorders",
            "Body as a Whole", "Ocular", "Cardiovascular", "Hepatic", "Renal", "Respiratory", "Dermatologic",
            "Gastrointestinal", "Neurologic", "Psychiatric", "Metabolic", "Musculoskeletal", "Hematologic"
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
        /// <param name="placeboArmClassifier">Shared placebo-arm classifier.</param>
        public ClaudeApiCorrectionService(
            HttpClient httpClient,
            IOptions<ClaudeApiCorrectionSettings> settings,
            ILogger<ClaudeApiCorrectionService> logger,
            IPlaceboArmClassifier? placeboArmClassifier = null)
        {
            #region implementation

            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
            _placeboArmClassifier = placeboArmClassifier ?? new PlaceboArmClassifier();

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
                _logger.LogDebug("Claude correction enabled but API key is empty — skipping correction");
                return observations;
            }

            // Gate by deterministic parse-quality score. Observations whose
            // QC_PARSE_QUALITY:{score} value is < ClaudeReviewQualityThreshold are
            // forwarded to Claude; observations at or above the threshold skip the API
            // correction pass. Observations without a quality flag (e.g., the
            // ParseQualityService is not registered) pass through conservatively.
            var toCorrect = _settings.ClaudeReviewQualityThreshold > 0f
                ? observations.Where(belowQualityThreshold).ToList()
                : observations;

            if (toCorrect.Count == 0)
            {
                _logger.LogDebug("All {Count} observations at or above parse-quality threshold {Threshold} — skipping Claude correction",
                    observations.Count, _settings.ClaudeReviewQualityThreshold);
                return observations;
            }

            if (toCorrect.Count < observations.Count)
            {
                _logger.LogInformation("Quality gate: {Passed}/{Total} observations below parse-quality threshold {Threshold} — sending to Claude",
                    toCorrect.Count, observations.Count, _settings.ClaudeReviewQualityThreshold);
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
                var correctionContext = CorrectionContext.FromTable(groupTable);

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
                        var applied = applyCorrections(chunk, corrections, correctionContext);
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
                        _logger.LogDebug(ex,
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
            // The regex replaces them with null before deserialization.
            var sanitized = sanitizeJsonFloatLiterals(json);

            // FloatParseHandling.Double (default) parses any bare NaN/Infinity tokens the
            // regex misses as double.NaN / double.PositiveInfinity. Downstream toSafeFloat()
            // already clamps NaN/Infinity to 0f, so these are harmless rather than fatal.
            var settings = new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Double };

            try
            {
                return JsonConvert.DeserializeObject<List<CorrectionEntry>>(sanitized, settings)
                    ?? new List<CorrectionEntry>();
            }
            catch (JsonException) when (wasTruncated)
            {
                // Response was truncated — try to salvage complete objects
                return salvageTruncatedJson(sanitized, settings);
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
        /// <param name="settings">Serializer settings (e.g. FloatParseHandling) from the caller.</param>
        /// <returns>List of corrections that could be recovered, may be empty.</returns>
        private List<CorrectionEntry> salvageTruncatedJson(string truncatedJson, JsonSerializerSettings settings)
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
                var result = JsonConvert.DeserializeObject<List<CorrectionEntry>>(salvaged, settings)
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
                o.Dose,
                o.DoseUnit,
                o.Population,
                o.Subpopulation,
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
        /// Applies validated corrections to in-memory observations and returns the count
        /// of accepted field mutations.
        /// </summary>
        /// <param name="observations">Observations to correct.</param>
        /// <param name="corrections">Corrections from Claude.</param>
        /// <param name="context">Source table context for header and percent-column checks.</param>
        /// <returns>Number of accepted field mutations.</returns>
        private int applyCorrections(
            List<ParsedObservation> observations,
            List<CorrectionEntry> corrections,
            CorrectionContext context)
        {
            #region implementation

            var applied = 0;

            foreach (var correction in corrections.OrderBy(c =>
                         string.Equals(c.Field, "Unit", StringComparison.OrdinalIgnoreCase) ? 1 : 0))
            {
                if (!CorrectableFields.Contains(correction.Field ?? string.Empty))
                {
                    _logger.LogDebug("Ignoring correction for non-correctable field: {Field}", correction.Field);
                    continue;
                }

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

                var proposedValue = normalizeCorrectionValue(correction.NewValue);
                var field = correction.Field!;
                var originalValue = getFieldValue(target, field);

                if (!tryValidateCorrection(target, field, proposedValue, context, out var rejectionReason))
                {
                    appendFlag(target, $"AI_REJECTED:{field}:{rejectionReason}");
                    _logger.LogDebug(
                        "Rejected Claude correction for TextTableID={TableId}, Row={Row}, Cell={Cell}, Field={Field}: '{Old}' -> '{New}' ({Reason})",
                        target.TextTableID, target.SourceRowSeq, target.SourceCellSeq, field,
                        originalValue, proposedValue, rejectionReason);
                    continue;
                }

                if (setFieldValue(target, field, proposedValue))
                {
                    appendFlag(target, $"AI_CORRECTED:{field}");

                    if (string.Equals(field, "TableCategory", StringComparison.OrdinalIgnoreCase))
                    {
                        appendFlag(target, $"CATEGORY_CLAUDE_CORRECTED:{originalValue ?? string.Empty}:{proposedValue ?? string.Empty}:1.00");
                    }

                    applied++;

                    _logger.LogDebug(
                        "Corrected {Field}: '{Old}' -> '{New}' (Row={Row}, Cell={Cell}, Reason={Reason})",
                        field, originalValue, proposedValue,
                        correction.SourceRowSeq, correction.SourceCellSeq, correction.Reason);
                }
            }

            if (_settings.EnforcePercentColumnConsistency)
            {
                applied += applyPercentColumnConsistency(observations, context);
            }

            return applied;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Represents source-table context needed by deterministic correction guards.
        /// </summary>
        private sealed class CorrectionContext
        {
            #region Fields

            /**************************************************************/
            /// <summary>Normalized source header tokens.</summary>
            private readonly HashSet<string> _headerTokens = new(StringComparer.OrdinalIgnoreCase);

            /**************************************************************/
            /// <summary>One-based source cell positions whose header path indicates percent values.</summary>
            private readonly HashSet<int> _percentColumns = new();

            #endregion

            #region Factory

            /**************************************************************/
            /// <summary>
            /// Builds correction context from a reconstructed table.
            /// </summary>
            /// <param name="table">Source reconstructed table, or null when unavailable.</param>
            /// <returns>Correction context with header tokens and percent-column positions.</returns>
            public static CorrectionContext FromTable(ReconstructedTable? table)
            {
                #region implementation

                var context = new CorrectionContext();
                if (table?.Rows == null)
                    return context;

                foreach (var row in table.Rows.Where(isHeaderRow))
                {
                    if (row.Cells == null)
                        continue;

                    foreach (var cell in row.Cells)
                    {
                        var text = cell.CleanedText ?? cell.RawCellText;
                        var normalized = normalizeTextToken(text);
                        if (!string.IsNullOrEmpty(normalized))
                        {
                            context._headerTokens.Add(normalized);
                        }

                        if (text?.Contains('%') == true)
                        {
                            context.addPercentColumn(cell);
                        }
                    }
                }

                return context;

                #endregion
            }

            #endregion

            #region Public Methods

            /**************************************************************/
            /// <summary>
            /// Returns true when <paramref name="value"/> exactly matches a source header token
            /// after whitespace normalization.
            /// </summary>
            /// <param name="value">Candidate value.</param>
            /// <returns>True for exact normalized header-token matches.</returns>
            public bool IsHeaderToken(string? value)
            {
                #region implementation

                var normalized = normalizeTextToken(value);
                return !string.IsNullOrEmpty(normalized) && _headerTokens.Contains(normalized);

                #endregion
            }

            /**************************************************************/
            /// <summary>
            /// Returns true when the observation source cell sits under a percent header.
            /// </summary>
            /// <param name="sourceCellSeq">One-based source cell sequence.</param>
            /// <returns>True when percent-column metadata exists for this cell sequence.</returns>
            public bool IsPercentColumn(int? sourceCellSeq)
            {
                #region implementation

                return sourceCellSeq.HasValue && _percentColumns.Contains(sourceCellSeq.Value);

                #endregion
            }

            #endregion

            #region Private Methods

            /**************************************************************/
            /// <summary>
            /// Adds all known one-based positions for a percent-bearing header cell.
            /// </summary>
            /// <param name="cell">Header cell.</param>
            private void addPercentColumn(ProcessedCell cell)
            {
                #region implementation

                if (cell.SequenceNumber.HasValue)
                {
                    _percentColumns.Add(cell.SequenceNumber.Value);
                }

                if (cell.ResolvedColumnStart.HasValue)
                {
                    var start = cell.ResolvedColumnStart.Value;
                    var end = cell.ResolvedColumnEnd ?? start + 1;
                    for (var column = start; column < end; column++)
                    {
                        _percentColumns.Add(column + 1);
                    }
                }

                #endregion
            }

            /**************************************************************/
            /// <summary>
            /// Determines whether a reconstructed row should contribute header context.
            /// </summary>
            /// <param name="row">Candidate reconstructed row.</param>
            /// <returns>True for explicit, inferred, and continuation header rows.</returns>
            private static bool isHeaderRow(ReconstructedRow row)
            {
                #region implementation

                if (string.Equals(row.RowGroupType, "Header", StringComparison.OrdinalIgnoreCase))
                    return true;

                return row.Classification is RowClassification.ExplicitHeader
                    or RowClassification.InferredHeader
                    or RowClassification.ContinuationHeader;

                #endregion
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates a proposed correction against deterministic guardrails.
        /// </summary>
        /// <param name="observation">Target observation.</param>
        /// <param name="field">Correction field.</param>
        /// <param name="proposedValue">Proposed value after NULL normalization.</param>
        /// <param name="context">Source table correction context.</param>
        /// <param name="rejectionReason">Reason token when validation fails.</param>
        /// <returns>True when the correction can be applied.</returns>
        private bool tryValidateCorrection(
            ParsedObservation observation,
            string field,
            string? proposedValue,
            CorrectionContext context,
            out string rejectionReason)
        {
            #region implementation

            rejectionReason = string.Empty;

            if (_settings.ProtectedFields.Contains(field))
            {
                rejectionReason = "ProtectedField";
                return false;
            }

            if (string.Equals(field, "TreatmentArm", StringComparison.OrdinalIgnoreCase))
            {
                return tryValidateTreatmentArmCorrection(observation, proposedValue, context, out rejectionReason);
            }

            if (string.Equals(field, "ParameterName", StringComparison.OrdinalIgnoreCase)
                && _settings.RejectParameterNameSuperset
                && isStrictTokenSuperset(proposedValue, observation.ParameterName))
            {
                rejectionReason = "ParameterNameSuperset";
                return false;
            }

            if (string.Equals(field, "PrimaryValueType", StringComparison.OrdinalIgnoreCase)
                && _settings.EnforcePercentColumnConsistency
                && context.IsPercentColumn(observation.SourceCellSeq)
                && string.Equals(observation.PrimaryValueType, "Percentage", StringComparison.OrdinalIgnoreCase)
                && string.Equals(proposedValue, "Count", StringComparison.OrdinalIgnoreCase))
            {
                rejectionReason = "PercentColumnTypeDemotion";
                return false;
            }

            if (string.Equals(field, "Unit", StringComparison.OrdinalIgnoreCase)
                && _settings.RejectTextRowUnitPercent
                && string.Equals(proposedValue, "%", StringComparison.Ordinal)
                && string.Equals(observation.PrimaryValueType, "Text", StringComparison.OrdinalIgnoreCase))
            {
                rejectionReason = "TextRowUnitPercent";
                return false;
            }

            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates TreatmentArm-specific correction guardrails.
        /// </summary>
        /// <param name="observation">Target observation.</param>
        /// <param name="proposedValue">Proposed TreatmentArm value.</param>
        /// <param name="context">Source table correction context.</param>
        /// <param name="rejectionReason">Reason token when validation fails.</param>
        /// <returns>True when the TreatmentArm correction can be applied.</returns>
        private bool tryValidateTreatmentArmCorrection(
            ParsedObservation observation,
            string? proposedValue,
            CorrectionContext context,
            out string rejectionReason)
        {
            #region implementation

            rejectionReason = string.Empty;
            var originalValue = observation.TreatmentArm;

            if (_settings.RejectPlaceboClassFlip
                && _placeboArmClassifier.IsPlaceboArm(originalValue, observation.Dose) !=
                   _placeboArmClassifier.IsPlaceboArm(proposedValue, observation.Dose))
            {
                rejectionReason = "PlaceboClassFlip";
                return false;
            }

            if (_settings.RejectTreatmentArmToNullUnlessHeaderEcho
                && !string.IsNullOrWhiteSpace(originalValue)
                && string.IsNullOrWhiteSpace(proposedValue)
                && (isProtectedShortTreatmentArm(originalValue)
                    || !isHeaderOrGenericTreatmentArm(originalValue, context)))
            {
                rejectionReason = "TreatmentArmNull";
                return false;
            }

            if (_settings.RejectTreatmentArmBodySystem && isBodySystemTreatmentArm(proposedValue))
            {
                rejectionReason = "TreatmentArmBodySystem";
                return false;
            }

            if (_settings.RejectTreatmentArmHeaderToken && context.IsHeaderToken(proposedValue))
            {
                rejectionReason = "TreatmentArmHeaderToken";
                return false;
            }

            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Enforces percent-column consistency after all accepted Claude corrections have
        /// been applied.
        /// </summary>
        /// <param name="observations">Chunk observations.</param>
        /// <param name="context">Source table correction context.</param>
        /// <returns>Number of field mutations applied.</returns>
        private static int applyPercentColumnConsistency(List<ParsedObservation> observations, CorrectionContext context)
        {
            #region implementation

            var applied = 0;

            foreach (var observation in observations)
            {
                if (!context.IsPercentColumn(observation.SourceCellSeq) || !isNumericObservation(observation))
                    continue;

                if (!string.Equals(observation.PrimaryValueType, "Percentage", StringComparison.OrdinalIgnoreCase))
                {
                    observation.PrimaryValueType = "Percentage";
                    appendFlag(observation, "AI_CORRECTED:PrimaryValueType");
                    applied++;
                }

                if (!string.Equals(observation.Unit, "%", StringComparison.Ordinal))
                {
                    observation.Unit = "%";
                    appendFlag(observation, "AI_CORRECTED:Unit");
                    applied++;
                }
            }

            return applied;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns the current observation value for a correctable field.
        /// </summary>
        /// <param name="obs">Observation.</param>
        /// <param name="fieldName">Correctable field name.</param>
        /// <returns>String value, or null when the field is unset or unsupported.</returns>
        private static string? getFieldValue(ParsedObservation obs, string fieldName)
        {
            #region implementation

            return ParsedObservationFieldAccess.GetAsString(obs, fieldName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts Claude's string literal NULL into a real null value.
        /// </summary>
        /// <param name="value">Raw correction value.</param>
        /// <returns>Normalized correction value.</returns>
        private static string? normalizeCorrectionValue(string? value)
        {
            #region implementation

            return string.Equals(value?.Trim(), "NULL", StringComparison.OrdinalIgnoreCase)
                ? null
                : value;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes text for exact equality checks.
        /// </summary>
        /// <param name="value">Source text.</param>
        /// <returns>Trimmed text with internal whitespace collapsed.</returns>
        private static string normalizeTextToken(string? value)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return WhitespacePattern.Replace(value.Trim(), " ");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a TreatmentArm value is protected as a short real arm token.
        /// </summary>
        /// <param name="value">TreatmentArm value.</param>
        /// <returns>True when the configured short-arm allowlist contains the value.</returns>
        private bool isProtectedShortTreatmentArm(string? value)
        {
            #region implementation

            return !string.IsNullOrWhiteSpace(value)
                && _settings.ProtectedShortTreatmentArms.Any(v =>
                    string.Equals(v, value.Trim(), StringComparison.OrdinalIgnoreCase));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether an existing TreatmentArm value is a known header or generic
        /// label echo that may safely be cleared.
        /// </summary>
        /// <param name="value">TreatmentArm value.</param>
        /// <param name="context">Source table correction context.</param>
        /// <returns>True when the value is structural rather than a real arm.</returns>
        private static bool isHeaderOrGenericTreatmentArm(string? value, CorrectionContext context)
        {
            #region implementation

            var normalized = normalizeTextToken(value);
            if (string.IsNullOrEmpty(normalized))
                return false;

            if (context.IsHeaderToken(normalized))
                return true;

            var lower = normalized.ToLowerInvariant();
            return (lower.Contains("number") && lower.Contains("patients"))
                || (lower.Contains("percent") && lower.Contains("subjects"))
                || (lower.Contains("percentage") && lower.Contains("reporting"))
                || lower is "comparison" or "treatment" or "pd" or "sad";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a proposed TreatmentArm is actually a body-system label.
        /// </summary>
        /// <param name="value">Proposed TreatmentArm value.</param>
        /// <returns>True for configured SOC/body-system labels.</returns>
        private static bool isBodySystemTreatmentArm(string? value)
        {
            #region implementation

            var normalized = normalizeTextToken(value);
            return !string.IsNullOrEmpty(normalized) && BodySystemTreatmentArmLabels.Contains(normalized);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a proposed name strictly adds tokens to the original name.
        /// </summary>
        /// <param name="proposedValue">Proposed ParameterName.</param>
        /// <param name="originalValue">Original ParameterName.</param>
        /// <returns>True when proposed tokens are a strict superset of original tokens.</returns>
        private static bool isStrictTokenSuperset(string? proposedValue, string? originalValue)
        {
            #region implementation

            var proposedTokens = tokenizeClinicalText(proposedValue);
            var originalTokens = tokenizeClinicalText(originalValue);

            return originalTokens.Count > 0
                && proposedTokens.Count > originalTokens.Count
                && proposedTokens.IsSupersetOf(originalTokens);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tokenizes clinical text for conservative set comparisons.
        /// </summary>
        /// <param name="value">Text to tokenize.</param>
        /// <returns>Lowercase alphanumeric token set.</returns>
        private static HashSet<string> tokenizeClinicalText(string? value)
        {
            #region implementation

            return WordTokenPattern.Matches(value ?? string.Empty)
                .Select(m => m.Value.ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether an observation is a numeric cell eligible for percent-column
        /// normalization.
        /// </summary>
        /// <param name="observation">Observation.</param>
        /// <returns>True when the observation contains a parsed or simple raw numeric value.</returns>
        private static bool isNumericObservation(ParsedObservation observation)
        {
            #region implementation

            if (observation.PrimaryValue.HasValue)
                return true;

            return !string.IsNullOrWhiteSpace(observation.RawValue)
                && SimpleNumericCellPattern.IsMatch(observation.RawValue.Replace(",", string.Empty));

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

            return ParsedObservationFieldAccess.SetFromString(obs, fieldName, value);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether an observation's parse-quality score is below the configured
        /// Claude-review threshold. Returns <c>true</c> (forward to Claude) when the parsed
        /// score is strictly less than
        /// <see cref="ClaudeApiCorrectionSettings.ClaudeReviewQualityThreshold"/>, or when the
        /// score is absent / unparseable / NaN / Infinity (conservative — forward when quality
        /// is unknown). Returns <c>false</c> only when a valid numeric score meets or exceeds
        /// the threshold, meaning the row parsed cleanly and does not need AI review.
        /// </summary>
        /// <remarks>
        /// The flag shape is <c>QC_PARSE_QUALITY:{score:F4}</c>, emitted by
        /// <see cref="IParseQualityService"/> in Stage 3.4. A companion
        /// <c>QC_PARSE_QUALITY:REVIEW_REASONS:{pipe-delimited list}</c> flag is emitted
        /// alongside when the score is below threshold and records which rule penalties fired
        /// — this method does not read the reasons, only the numeric score on the primary flag.
        /// </remarks>
        /// <param name="obs">Observation to evaluate.</param>
        /// <returns><c>true</c> if the observation should be sent to Claude.</returns>
        private bool belowQualityThreshold(ParsedObservation obs)
        {
            #region implementation

            if (string.IsNullOrEmpty(obs.ValidationFlags))
                return true; // No flags at all → conservative: send to Claude

            // Find the QC_PARSE_QUALITY:{score} token. The REVIEW_REASONS variant is a
            // separate flag with the same prefix plus ":REVIEW_REASONS:"; scan for a numeric
            // token that is not that sentinel.
            const string prefix = "QC_PARSE_QUALITY:";
            var search = 0;
            while (search < obs.ValidationFlags.Length)
            {
                var startIdx = obs.ValidationFlags.IndexOf(prefix, search, StringComparison.Ordinal);
                if (startIdx < 0)
                    return true; // No quality flag at all → conservative: send to Claude

                var valueStart = startIdx + prefix.Length;
                var valueEnd = obs.ValidationFlags.IndexOf(';', valueStart);
                var tokenEnd = valueEnd >= 0 ? valueEnd : obs.ValidationFlags.Length;
                var token = obs.ValidationFlags.Substring(valueStart, tokenEnd - valueStart).Trim();

                // Skip the REVIEW_REASONS companion flag — keep scanning for the numeric one.
                if (token.StartsWith("REVIEW_REASONS", StringComparison.OrdinalIgnoreCase))
                {
                    search = tokenEnd;
                    continue;
                }

                if (float.TryParse(token, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var score))
                {
                    // NaN / Infinity → conservative: send to Claude
                    if (float.IsNaN(score) || float.IsInfinity(score))
                        return true;
                    return score < _settings.ClaudeReviewQualityThreshold;
                }

                // Unparseable token → conservative: send to Claude
                return true;
            }

            // Scanned past end without finding a numeric score → conservative: send to Claude
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the JSON correction array from Claude's response text, tolerating
        /// arbitrary surrounding content — markdown code fences, explanatory prose, or
        /// stray backticks left behind by a partial fence. Locates the first <c>[</c>
        /// and walks forward (tracking string state, escape sequences, and bracket
        /// nesting) until the matching <c>]</c> is found, returning only that substring.
        /// </summary>
        /// <remarks>
        /// Replaces an earlier prefix/suffix fence strip that broke whenever Claude
        /// added trailing prose or a dangling backtick on its own line — producing
        /// <c>Newtonsoft.Json.JsonReaderException: Additional text encountered after
        /// finished reading JSON content</c> at deserialization time. String contents
        /// may legitimately contain <c>[</c> or <c>]</c> (e.g. in a <c>reason</c> field),
        /// and escaped quotes (<c>\"</c>) must not toggle the in-string flag — both are
        /// handled by the single-pass state machine below. If the array is unbalanced
        /// (truncated response), the substring from the first <c>[</c> onward is
        /// returned so <see cref="salvageTruncatedJson"/> can recover complete entries.
        /// </remarks>
        /// <param name="text">Raw response text from Claude.</param>
        /// <returns>The extracted JSON array substring, or the trimmed text if no array is found.</returns>
        /// <seealso cref="deserializeCorrections"/>
        /// <seealso cref="salvageTruncatedJson"/>
        private static string stripMarkdownFences(string text)
        {
            #region implementation

            var trimmed = text.Trim();

            // Locate the start of the JSON correction array.
            var firstBracket = trimmed.IndexOf('[');
            if (firstBracket < 0)
                return trimmed;

            // Walk forward from the opening bracket, tracking string state and nesting
            // depth, to find the matching closing bracket. Everything outside the
            // balanced array (leading prose, trailing fences/prose) is discarded.
            var depth = 0;
            var inString = false;
            var escaped = false;

            for (int i = firstBracket; i < trimmed.Length; i++)
            {
                var c = trimmed[i];

                if (escaped)
                {
                    // Previous char was a backslash inside a string — consume this char literally.
                    escaped = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    // Begin an escape sequence; the next char is skipped for state purposes.
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    // Toggle string state. Escaped quotes are handled by the escaped branch above.
                    inString = !inString;
                    continue;
                }

                // Brackets inside string literals must not affect depth.
                if (inString) continue;

                if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        // Found the matching close of the outermost array.
                        return trimmed.Substring(firstBracket, i - firstBracket + 1);
                    }
                }
            }

            // Unbalanced — likely a truncated response. Return from the first bracket
            // onward so salvageTruncatedJson can recover any complete objects.
            return trimmed.Substring(firstBracket);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Sanitizes bare floating-point literals (<c>NaN</c>, <c>Infinity</c>, <c>-Infinity</c>)
        /// in JSON by replacing them with <c>null</c>. Claude occasionally emits these as unquoted
        /// values in correction entries (e.g., <c>"newValue": NaN</c>), which Newtonsoft.Json
        /// cannot parse as they are not valid JSON tokens.
        /// </summary>
        /// <remarks>
        /// Uses a zero-width lookahead for the trailing structural delimiter rather than a
        /// capturing group so the delimiter is never consumed. This allows consecutive NaN
        /// values (e.g., <c>[NaN,NaN]</c>) to both be replaced in a single pass — with a
        /// capturing group the comma after the first NaN would be consumed into Group 3 and
        /// become unavailable as the Group 1 delimiter for the second NaN. The lookahead also
        /// matches end-of-string to handle truncated payloads that end with a bare literal.
        /// </remarks>
        /// <param name="json">Raw JSON text from Claude.</param>
        /// <returns>Sanitized JSON with bare float literals replaced by <c>null</c>.</returns>
        private static string sanitizeJsonFloatLiterals(string json)
        {
            #region implementation

            // Replace bare NaN/Infinity tokens with null.
            // Group 1 captures the preceding structural delimiter (: , [) and optional whitespace.
            // Group 2 (the literal) is discarded.
            // Trailing lookahead asserts a structural delimiter or end-of-string without consuming it,
            // so adjacent NaN values sharing a comma separator are each matched correctly.
            return System.Text.RegularExpressions.Regex.Replace(
                json,
                @"([:,\[]\s*)(-?(?:NaN|Infinity))(?=\s*[,}\]]|\s*$)",
                "${1}null");

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
                _logger.LogDebug(
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
        /// Appends a flag to the observation's ValidationFlags, separated by <c>"; "</c>.
        /// Delegates to the shared <see cref="ValidationFlagExtensions.AppendValidationFlag"/>
        /// helper so the delimiter convention stays in one place across services.
        /// </summary>
        /// <param name="obs">Observation to flag.</param>
        /// <param name="flag">Flag to append.</param>
        private static void appendFlag(ParsedObservation obs, string flag) => obs.AppendValidationFlag(flag);

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
