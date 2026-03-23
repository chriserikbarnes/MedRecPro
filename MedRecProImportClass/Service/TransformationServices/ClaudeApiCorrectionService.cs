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
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The corrected observations (same list, mutated in place).</returns>
        Task<List<ParsedObservation>> CorrectBatchAsync(
            List<ParsedObservation> observations,
            CancellationToken ct = default);
    }

    #endregion

    #region implementation

    /**************************************************************/
    /// <summary>
    /// Claude API client that performs post-parse correction of <see cref="ParsedObservation"/>
    /// objects by sending them to Claude for semantic review. Identifies and corrects common
    /// parser misclassifications such as wrong PrimaryValueType, swapped TreatmentArm/ParameterName,
    /// or incorrect SecondaryValueType assignments.
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
        /// </summary>
        private static readonly HashSet<string> CorrectableFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "ParameterName", "PrimaryValueType", "SecondaryValueType",
            "TreatmentArm", "DoseRegimen", "Population", "Unit",
            "ParameterCategory", "ParameterSubtype"
        };

        /**************************************************************/
        /// <summary>
        /// System prompt for the correction skill. Concise instructions for Claude
        /// to identify and return corrections for misclassified parser output.
        /// </summary>
        private const string CorrectionSystemPrompt = @"You correct parsed pharmaceutical table observations. You receive a JSON array of parsed rows from a single FDA drug label table. Each row has been mechanically parsed from an HTML table.

            Your job: identify rows where the parser likely misclassified fields. Common errors:
            - PrimaryValueType wrong (e.g., ""Numeric"" should be ""Percentage"", ""Mean"" should be ""Median"")
            - ParameterName truncated or includes footnote markers
            - TreatmentArm and ParameterName swapped (row vs column confusion)
            - SecondaryValueType wrong (e.g., ""SD"" should be ""SE"", ""CV_Percent"" should be ""SD"")
            - DoseRegimen assigned to wrong field
            - Caption-derived hints not applied (e.g., table caption says ""Mean (SD)"" but values parsed as n(%))

            Return ONLY corrections as a JSON array. If no corrections needed, return [].
            Format: [{""sourceRowSeq"": N, ""sourceCellSeq"": N, ""field"": ""FieldName"", ""oldValue"": ""X"", ""newValue"": ""Y"", ""reason"": ""brief why""}]
            Correctable fields: ParameterName, PrimaryValueType, SecondaryValueType, TreatmentArm, DoseRegimen, Population, Unit, ParameterCategory, ParameterSubtype

            IMPORTANT: Return ONLY the JSON array. No markdown fences, no explanation text.";

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

            // Group by TextTableID for contextual correction
            var groups = observations.GroupBy(o => o.TextTableID ?? 0).ToList();
            var totalCorrections = 0;

            foreach (var group in groups)
            {
                ct.ThrowIfCancellationRequested();

                var tableObservations = group.ToList();

                // Split into sub-batches if needed
                var chunks = chunkList(tableObservations, _settings.MaxObservationsPerRequest);

                foreach (var chunk in chunks)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        var corrections = await requestCorrectionsAsync(chunk, ct);
                        var applied = applyCorrections(chunk, corrections);
                        totalCorrections += applied;
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
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of correction objects from the API response.</returns>
        private async Task<List<CorrectionEntry>> requestCorrectionsAsync(
            List<ParsedObservation> observations,
            CancellationToken ct)
        {
            #region implementation

            var payload = buildCompactPayload(observations);

            var requestBody = new
            {
                model = _settings.Model,
                max_tokens = _settings.MaxTokens,
                temperature = _settings.Temperature,
                system = CorrectionSystemPrompt,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = $"Table caption: {observations.FirstOrDefault()?.Caption ?? "(none)"}\nTable category: {observations.FirstOrDefault()?.TableCategory ?? "UNKNOWN"}\n\nParsed observations:\n{payload}"
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

            return JsonConvert.DeserializeObject<List<CorrectionEntry>>(textContent)
                ?? new List<CorrectionEntry>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a compact JSON payload containing only correction-relevant fields
        /// to minimize token usage.
        /// </summary>
        /// <param name="observations">Observations to serialize.</param>
        /// <returns>JSON string of trimmed observation data.</returns>
        private static string buildCompactPayload(List<ParsedObservation> observations)
        {
            #region implementation

            var compact = observations.Select(o => new
            {
                o.SourceRowSeq,
                o.SourceCellSeq,
                o.ParameterName,
                o.TreatmentArm,
                o.RawValue,
                o.PrimaryValue,
                o.PrimaryValueType,
                o.SecondaryValue,
                o.SecondaryValueType,
                o.TableCategory,
                o.ParseConfidence,
                o.ParseRule,
                o.Caption,
                o.DoseRegimen,
                o.Population,
                o.ArmN,
                o.Unit,
                o.ParameterCategory,
                o.ParameterSubtype
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
        /// </summary>
        /// <param name="obs">Target observation.</param>
        /// <param name="fieldName">Field to set (case-insensitive).</param>
        /// <param name="value">New value.</param>
        /// <returns>True if set successfully.</returns>
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
                default:
                    return false;
            }

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

        #endregion
    }

    #endregion

    #region correction entry DTO

    /**************************************************************/
    /// <summary>
    /// Represents a single field correction returned by the Claude API.
    /// </summary>
    internal class CorrectionEntry
    {
        /**************************************************************/
        /// <summary>Row sequence identifying the target observation.</summary>
        [JsonProperty("sourceRowSeq")]
        public int? SourceRowSeq { get; set; }

        /**************************************************************/
        /// <summary>Cell sequence identifying the target observation.</summary>
        [JsonProperty("sourceCellSeq")]
        public int? SourceCellSeq { get; set; }

        /**************************************************************/
        /// <summary>Field name to correct (must be in CorrectableFields set).</summary>
        [JsonProperty("field")]
        public string? Field { get; set; }

        /**************************************************************/
        /// <summary>Original value (for logging/audit).</summary>
        [JsonProperty("oldValue")]
        public string? OldValue { get; set; }

        /**************************************************************/
        /// <summary>Corrected value to apply.</summary>
        [JsonProperty("newValue")]
        public string? NewValue { get; set; }

        /**************************************************************/
        /// <summary>Brief reason for the correction.</summary>
        [JsonProperty("reason")]
        public string? Reason { get; set; }
    }

    #endregion
}
