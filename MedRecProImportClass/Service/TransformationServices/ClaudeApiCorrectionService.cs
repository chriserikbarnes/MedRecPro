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
        /// System prompt for the correction skill. Encodes the full normalization
        /// rule set from the table-parser-data-dictionary skill: column contracts,
        /// PrimaryValueType migration, DoseRegimen triage, Unit scrub, ParameterName
        /// and TreatmentArm cleanup, ParameterCategory SOC mapping, and BoundType
        /// inference. Rules are ordered by priority within each section.
        /// </summary>
        /// <remarks>
        /// Source: column-contracts.md, normalization-rules.md, table-types.md
        /// (table-parser-data-dictionary skill, references/ folder).
        /// Update this prompt whenever those reference files change.
        /// </remarks>
        private const string CorrectionSystemPrompt = @"You review parsed pharmaceutical SPL label table observations for CLEAR errors only.

## Rules
- Only flag OBVIOUS misclassifications. If uncertain, do NOT correct.
- Max 15 corrections per batch. Prioritize highest-impact errors.
- Keep ""reason"" to 6 words max.
- Return ONLY a JSON array. No markdown. No explanation outside the array.
- If nothing is clearly wrong, return [].

## TableCategory — the governing context for all rules
AdverseEvent | PK | DrugInteraction | Efficacy | Dosing | BMD | TissueDistribution | Demographic | Laboratory | TextDescriptive | Unclassified

## PrimaryValueType — valid values (15)
ArithmeticMean | GeometricMean | GeometricMeanRatio | LSMean | Median | Proportion | Count | PercentChange | HazardRatio | OddsRatio | RelativeRisk | RiskDifference | PValue | Text | Numeric

Migrations (correct these old values):
- ""Mean"" → GeometricMean when TableCategory=PK or DrugInteraction (unless caption has ""arithmetic"")
- ""Mean"" → ArithmeticMean when TableCategory=AdverseEvent, or caption has ""arithmetic"", or no other context
- ""Percentage"" → Proportion (Unit should be ""%"")
- ""MeanPercentChange"" → PercentChange
- ""RelativeRiskReduction"" → HazardRatio (caption has ""hazard""), OddsRatio (caption has ""odds""), else RelativeRisk
- ""Numeric"" (AdverseEvent, Unit=""%"") → Proportion
- ""Numeric"" (AdverseEvent, Unit null, value is integer) → Count
- ""Numeric"" (PK) → GeometricMean
- ""Numeric"" (DrugInteraction, no bounds) → GeometricMeanRatio
- ""Numeric"" (DrugInteraction, bounds present) → GeometricMeanRatio
- ""Numeric"" (BMD) → PercentChange
- ""Numeric"" (Efficacy, bounds present) → HazardRatio
- Caption has ""geometric mean"" → GeometricMean
- Caption has ""arithmetic mean"" → ArithmeticMean
- Caption has ""LS mean"" or ""least square"" → LSMean
- Caption has ""median"" → Median

## SecondaryValueType — valid values
SD | SE | CI | CV | IQR | Range | N
Check: ""SD"" vs ""SE"" contradicted by caption (""standard error"" → SE, ""standard deviation"" → SD).

## DoseRegimen — route misplaced content (first match wins)
1. PK sub-parameter name → field=DoseRegimen newValue=NULL, ALSO field=ParameterSubtype newValue={pk_param}
   Matches: Cmax, Cmin, Tmax, AUC*, t1/2, t½, CL/F, CL, V/F, Vss, Vd, ke, MRT, MAT, bioavailability, CV(%)
2. Actual dose (contains digit + mg|mcg|µg|g|mL|units|IU) → Keep — do NOT move
3. Drug name when TableCategory=DrugInteraction or PK → field=DoseRegimen newValue=NULL, ALSO field=ParameterSubtype newValue={drug_name}
4. Population pattern (adult|pediatric|elderly|renal|hepatic|healthy|volunteer) → field=DoseRegimen newValue=NULL, field=Population newValue={value}
5. Timepoint pattern (day \d|week \d|month \d|cycle \d|baseline|steady.state|single.dose|pre-?dose) → field=DoseRegimen newValue=NULL, field=Timepoint newValue={value}
6. Literal ""Co-administered Drug"" → field=DoseRegimen newValue=NULL (header echo)

## Unit — clear header leaks
- Length > 30 chars (not a real unit) → NULL
- Contains a drug name → NULL
- Contains any of: Regimen|Dosage|Patients|Titration|Starting|Recommended|Duration|TAKING|Tablets|Injection|Therapy|Combination|Divided → NULL
- Normalize variants: ""hr"" → ""h"", ""mcg h/mL"" → ""mcg·h/mL"", ""nghr/mL"" → ""ng·h/mL"", ""L/kghr"" → ""L/kg/h"", ""mcgh/mL"" → ""mcg·h/mL""

Valid units (≤15 chars typical): % %CV h min days mg mcg µg g kg mcg/mL ng/mL pg/mL µg/mL mg/L mcg·h/mL ng·h/mL mL/min L/h L/kg ratio g/cm² mmHg mEq/L IU/mL mg/kg mg/m²

## ParameterName — route misplaced content
1. Starts with ""Table \d"" or contains caption echo (""Pharmacokinetic Parameters"", ""Geometric Mean Ratio"", ""Drug Interactions:"") or len > 60 → NULL, reason=ROW_TYPE=CAPTION
2. Exact match ^n$ or ^N$ or starts with ""n ("" or ""N ("" → NULL, reason=ROW_TYPE=HEADER
3. Bare integer from common dose set {5,10,15,20,25,30,40,50,100,150,200,250,300,400,500,600,800,1200,1600,2400,3600} when TableCategory=Dosing or PK → field=ParameterName newValue=NULL, field=DoseRegimen newValue={integer}
4. Drug name (not a PK param) when TableCategory=DrugInteraction → field=ParameterName newValue=NULL, field=ParameterSubtype newValue={drug_name}
5. HTML entities (&gt; &lt; &amp;) → decode to > < &

## TreatmentArm — route misplaced content
1. Contains ""Number"" + ""Patients"" or ""Percent"" + ""Subjects"" or ""Percentage"" + ""Reporting"" → NULL (header echo)
2. Contains [N=xxx] or N=xxx → extract integer to ArmN (separate correction), strip pattern from arm
3. Embedded dose: arm = ""150 mg/d [N=302]"" → field=TreatmentArm newValue=""150 mg/d"" stripped, field=DoseRegimen newValue=dose
4. Value is Comparison|Treatment|PD|SAD → NULL (generic label)
5. All-caps short study name (SPRING-2, SINGLE, SAILING, ATLAS, ECHO, TRIO) → field=TreatmentArm newValue=NULL, field=StudyContext newValue={name}

## ParameterCategory — AdverseEvent and Laboratory tables only
Must be a canonical MedDRA SOC name. Correct OCR variants and informal names:
- cardiac disorders → Cardiac Disorders
- gastrointestinal|gastrointestinal disorders|digestive system → Gastrointestinal Disorders
- nervous system|cns|central & peripheral nervous system disorders → Nervous System Disorders
- musculo-skeletal|musculoskeletal and connective tissue → Musculoskeletal Disorders
- general disorders and administration site conditions|body as a whole → General Disorders
- skin|dermatologic|skin and subcutaneous tissues disorders → Skin and Subcutaneous Tissue Disorders
- respiratory system|respiratory, thoracic and mediastinal → Respiratory Disorders
- psychiatric → Psychiatric Disorders
- vascular disorders|cardiovascular → Vascular Disorders
- infections and infestations|resistance mechanism → Infections and Infestations
- renal and urinary|urogenital → Renal and Urinary Disorders
- hematologic|blood and lymphatic → Blood and Lymphatic System Disorders
- metabolism and nutrition|metabolic and nutritional → Metabolism and Nutrition Disorders
- hepatobiliary|liver and biliary → Hepatobiliary Disorders
- ear disorders|ear and labyrinth → Ear and Labyrinth Disorders
- eye disorders|special senses → Eye Disorders
For non-AdverseEvent/Laboratory tables: ParameterCategory should be NULL (do not correct to SOC).

## BoundType — infer when bounds present but BoundType is NULL
- TableCategory=PK or DrugInteraction → ""90CI""
- TableCategory=Efficacy or BMD → ""95CI""
- Any other category with bounds → ""95CI"" (safe default)

## Also check
- TreatmentArm and ParameterName swapped (arm contains PK/AE parameter name, ParameterName contains drug/arm name)
- TableCategory=DrugInteraction: ParameterSubtype should hold co-administered drug name, not be NULL

Format: [{""sourceRowSeq"":N,""sourceCellSeq"":N,""field"":""FieldName"",""oldValue"":""X"",""newValue"":""Y"",""reason"":""brief""}]
Correctable fields: ParameterName, PrimaryValueType, SecondaryValueType, TreatmentArm, DoseRegimen, Population, Unit, ParameterCategory, ParameterSubtype, Timepoint, TimeUnit, StudyContext, BoundType";

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

            // Build structured context header so Claude can apply the right per-category
            // rules without reading each observation's TableCategory field individually.
            var firstObs = observations.FirstOrDefault();
            var contextHeader = new StringBuilder();
            contextHeader.AppendLine($"TableCategory: {firstObs?.TableCategory ?? "UNKNOWN"}");
            contextHeader.AppendLine($"ParentSectionCode: {firstObs?.ParentSectionCode ?? "(none)"}");
            contextHeader.AppendLine($"Caption: {firstObs?.Caption ?? "(none)"}");
            contextHeader.AppendLine($"ObservationCount: {observations.Count}");

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

            try
            {
                return JsonConvert.DeserializeObject<List<CorrectionEntry>>(json)
                    ?? new List<CorrectionEntry>();
            }
            catch (JsonException) when (wasTruncated)
            {
                // Response was truncated — try to salvage complete objects
                return salvageTruncatedJson(json);
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
