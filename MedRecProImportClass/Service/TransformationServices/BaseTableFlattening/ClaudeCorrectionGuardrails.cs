using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening;
using System.Text.RegularExpressions;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Represents source-table context needed by deterministic Claude correction guardrails.
    /// </summary>
    /// <remarks>
    /// The context keeps header-token and percent-column facts outside
    /// <see cref="ClaudeApiCorrectionService"/> so guardrails can be tested without making
    /// HTTP calls or deserializing correction payloads.
    /// </remarks>
    /// <seealso cref="ClaudeApiCorrectionService"/>
    /// <seealso cref="CorrectionGuardrailChain"/>
    internal sealed class ClaudeCorrectionContext
    {
        #region Fields

        /**************************************************************/
        /// <summary>
        /// Regex used to collapse whitespace for exact token comparisons.
        /// </summary>
        private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);

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
        public static ClaudeCorrectionContext FromTable(ReconstructedTable? table)
        {
            #region implementation

            var context = new ClaudeCorrectionContext();
            if (table?.Rows == null)
                return context;

            foreach (var row in table.Rows.Where(isHeaderRow))
            {
                if (row.Cells == null)
                    continue;

                foreach (var cell in row.Cells)
                {
                    var text = cell.CleanedText ?? cell.RawCellText;
                    var normalized = NormalizeTextToken(text);
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

            var normalized = NormalizeTextToken(value);
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

        /**************************************************************/
        /// <summary>
        /// Normalizes text for exact equality checks.
        /// </summary>
        /// <param name="value">Source text.</param>
        /// <returns>Trimmed text with internal whitespace collapsed.</returns>
        internal static string NormalizeTextToken(string? value)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return WhitespacePattern.Replace(value.Trim(), " ");

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
    /// Request object passed through each Claude correction guardrail.
    /// </summary>
    /// <remarks>
    /// Per-message state is held here so individual guardrails stay stateless and can be
    /// added, removed, or reordered by <see cref="CorrectionGuardrailChain"/>.
    /// </remarks>
    /// <seealso cref="CorrectionGuardrailChain"/>
    /// <seealso cref="ICorrectionGuardrail"/>
    internal sealed class CorrectionGuardrailRequest
    {
        /**************************************************************/
        /// <summary>Target observation being corrected.</summary>
        public ParsedObservation Observation { get; }

        /**************************************************************/
        /// <summary>Correction field name.</summary>
        public string Field { get; }

        /**************************************************************/
        /// <summary>Proposed value after Claude NULL-literal normalization.</summary>
        public string? ProposedValue { get; }

        /**************************************************************/
        /// <summary>Source table facts used by header and percent-column guards.</summary>
        public ClaudeCorrectionContext SourceContext { get; }

        /**************************************************************/
        /// <summary>Correction service settings controlling optional guardrails.</summary>
        public ClaudeApiCorrectionSettings Settings { get; }

        /**************************************************************/
        /// <summary>Shared placebo classifier used for TreatmentArm semantic checks.</summary>
        public IPlaceboArmClassifier PlaceboArmClassifier { get; }

        /**************************************************************/
        /// <summary>
        /// Initializes a guardrail request.
        /// </summary>
        /// <param name="observation">Target observation being corrected.</param>
        /// <param name="field">Correction field name.</param>
        /// <param name="proposedValue">Proposed value after NULL-literal normalization.</param>
        /// <param name="sourceContext">Source table facts.</param>
        /// <param name="settings">Correction settings.</param>
        /// <param name="placeboArmClassifier">Shared placebo classifier.</param>
        public CorrectionGuardrailRequest(
            ParsedObservation observation,
            string field,
            string? proposedValue,
            ClaudeCorrectionContext sourceContext,
            ClaudeApiCorrectionSettings settings,
            IPlaceboArmClassifier placeboArmClassifier)
        {
            #region implementation

            Observation = observation;
            Field = field;
            ProposedValue = proposedValue;
            SourceContext = sourceContext;
            Settings = settings;
            PlaceboArmClassifier = placeboArmClassifier;

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Operation-result style return value from a Claude correction guardrail.
    /// </summary>
    /// <remarks>
    /// Accepted results carry no reason. Rejected results carry the exact validation-flag
    /// reason token appended by <see cref="ClaudeApiCorrectionService"/>.
    /// </remarks>
    /// <seealso cref="CorrectionGuardrailChain"/>
    internal sealed class CorrectionGuardrailResult
    {
        /**************************************************************/
        /// <summary>Shared accepted result.</summary>
        private static readonly CorrectionGuardrailResult AcceptedResult = new(true, string.Empty);

        /**************************************************************/
        /// <summary>True when the correction should continue through the chain.</summary>
        public bool IsAccepted { get; }

        /**************************************************************/
        /// <summary>Exact reason token to append when rejected.</summary>
        public string RejectionReason { get; }

        /**************************************************************/
        /// <summary>
        /// Initializes a guardrail result.
        /// </summary>
        /// <param name="isAccepted">Whether the correction is accepted.</param>
        /// <param name="rejectionReason">Exact rejection reason token.</param>
        private CorrectionGuardrailResult(bool isAccepted, string rejectionReason)
        {
            #region implementation

            IsAccepted = isAccepted;
            RejectionReason = rejectionReason;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an accepted result.
        /// </summary>
        /// <returns>Accepted guardrail result.</returns>
        public static CorrectionGuardrailResult Accept()
        {
            #region implementation

            return AcceptedResult;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a rejected result with an exact reason token.
        /// </summary>
        /// <param name="reason">Exact reason token.</param>
        /// <returns>Rejected guardrail result.</returns>
        public static CorrectionGuardrailResult Reject(string reason)
        {
            #region implementation

            return new CorrectionGuardrailResult(false, reason);

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Validates one Claude correction proposal.
    /// </summary>
    /// <remarks>
    /// Implementations are stateless chain handlers. They either accept and let later
    /// guardrails inspect the request, or reject with the exact reason token used in
    /// validation flags.
    /// </remarks>
    /// <seealso cref="CorrectionGuardrailChain"/>
    internal interface ICorrectionGuardrail
    {
        /**************************************************************/
        /// <summary>
        /// Validates a correction request.
        /// </summary>
        /// <param name="request">Correction request.</param>
        /// <returns>Accepted or rejected guardrail result.</returns>
        CorrectionGuardrailResult Validate(CorrectionGuardrailRequest request);
    }

    /**************************************************************/
    /// <summary>
    /// Ordered Chain of Responsibility for Claude correction validation.
    /// </summary>
    /// <remarks>
    /// The chain preserves the prior short-circuit behavior from
    /// <see cref="ClaudeApiCorrectionService"/>: the first rejected guardrail supplies the
    /// reason token and later guards do not run.
    /// </remarks>
    /// <seealso cref="ICorrectionGuardrail"/>
    /// <seealso cref="CorrectionGuardrailResult"/>
    internal sealed class CorrectionGuardrailChain
    {
        #region Fields

        /**************************************************************/
        /// <summary>Correction service settings.</summary>
        private readonly ClaudeApiCorrectionSettings _settings;

        /**************************************************************/
        /// <summary>Shared placebo-arm classifier.</summary>
        private readonly IPlaceboArmClassifier _placeboArmClassifier;

        /**************************************************************/
        /// <summary>Ordered guardrail list. First rejection wins.</summary>
        private readonly IReadOnlyList<ICorrectionGuardrail> _guardrails;

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes the ordered correction guardrail chain.
        /// </summary>
        /// <param name="settings">Correction service settings.</param>
        /// <param name="placeboArmClassifier">Shared placebo-arm classifier.</param>
        public CorrectionGuardrailChain(
            ClaudeApiCorrectionSettings settings,
            IPlaceboArmClassifier placeboArmClassifier)
        {
            #region implementation

            _settings = settings;
            _placeboArmClassifier = placeboArmClassifier;
            _guardrails = new ICorrectionGuardrail[]
            {
                new ProtectedFieldGuardrail(),
                new TreatmentArmPlaceboClassGuardrail(),
                new TreatmentArmNullGuardrail(),
                new TreatmentArmBodySystemGuardrail(),
                new TreatmentArmHeaderTokenGuardrail(),
                new ParameterNameSupersetGuardrail(),
                new PercentColumnTypeDemotionGuardrail(),
                new TextRowUnitPercentGuardrail()
            };

            #endregion
        }

        #endregion

        #region Public Methods

        /**************************************************************/
        /// <summary>
        /// Validates a proposed correction through the ordered guardrail chain.
        /// </summary>
        /// <param name="observation">Target observation.</param>
        /// <param name="field">Correction field.</param>
        /// <param name="proposedValue">Proposed value after NULL-literal normalization.</param>
        /// <param name="sourceContext">Source table facts.</param>
        /// <returns>Accepted or rejected guardrail result.</returns>
        public CorrectionGuardrailResult Validate(
            ParsedObservation observation,
            string field,
            string? proposedValue,
            ClaudeCorrectionContext sourceContext)
        {
            #region implementation

            var request = new CorrectionGuardrailRequest(
                observation,
                field,
                proposedValue,
                sourceContext,
                _settings,
                _placeboArmClassifier);

            foreach (var guardrail in _guardrails)
            {
                var result = guardrail.Validate(request);
                if (!result.IsAccepted)
                    return result;
            }

            return CorrectionGuardrailResult.Accept();

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Rejects corrections targeting fields protected by configuration.
    /// </summary>
    /// <seealso cref="ICorrectionGuardrail"/>
    internal sealed class ProtectedFieldGuardrail : ICorrectionGuardrail
    {
        /**************************************************************/
        /// <inheritdoc/>
        public CorrectionGuardrailResult Validate(CorrectionGuardrailRequest request)
        {
            #region implementation

            return request.Settings.ProtectedFields.Contains(request.Field)
                ? CorrectionGuardrailResult.Reject("ProtectedField")
                : CorrectionGuardrailResult.Accept();

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Rejects TreatmentArm rewrites that change placebo-class semantics.
    /// </summary>
    /// <seealso cref="ICorrectionGuardrail"/>
    internal sealed class TreatmentArmPlaceboClassGuardrail : ICorrectionGuardrail
    {
        /**************************************************************/
        /// <inheritdoc/>
        public CorrectionGuardrailResult Validate(CorrectionGuardrailRequest request)
        {
            #region implementation

            if (!isTreatmentArm(request) || !request.Settings.RejectPlaceboClassFlip)
                return CorrectionGuardrailResult.Accept();

            var originalValue = request.Observation.TreatmentArm;
            var dose = request.Observation.Dose;
            return request.PlaceboArmClassifier.IsPlaceboArm(originalValue, dose) !=
                   request.PlaceboArmClassifier.IsPlaceboArm(request.ProposedValue, dose)
                ? CorrectionGuardrailResult.Reject("PlaceboClassFlip")
                : CorrectionGuardrailResult.Accept();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a guardrail request targets TreatmentArm.
        /// </summary>
        /// <param name="request">Correction request.</param>
        /// <returns>True for TreatmentArm corrections.</returns>
        private static bool isTreatmentArm(CorrectionGuardrailRequest request)
        {
            #region implementation

            return string.Equals(request.Field, "TreatmentArm", StringComparison.OrdinalIgnoreCase);

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Rejects unsafe TreatmentArm nulling unless the original value is structural.
    /// </summary>
    /// <seealso cref="ICorrectionGuardrail"/>
    internal sealed class TreatmentArmNullGuardrail : ICorrectionGuardrail
    {
        /**************************************************************/
        /// <inheritdoc/>
        public CorrectionGuardrailResult Validate(CorrectionGuardrailRequest request)
        {
            #region implementation

            if (!string.Equals(request.Field, "TreatmentArm", StringComparison.OrdinalIgnoreCase)
                || !request.Settings.RejectTreatmentArmToNullUnlessHeaderEcho)
            {
                return CorrectionGuardrailResult.Accept();
            }

            var originalValue = request.Observation.TreatmentArm;
            if (!string.IsNullOrWhiteSpace(originalValue)
                && string.IsNullOrWhiteSpace(request.ProposedValue)
                && (isProtectedShortTreatmentArm(originalValue, request.Settings)
                    || !isHeaderOrGenericTreatmentArm(originalValue, request.SourceContext)))
            {
                return CorrectionGuardrailResult.Reject("TreatmentArmNull");
            }

            return CorrectionGuardrailResult.Accept();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a TreatmentArm value is protected as a short real arm token.
        /// </summary>
        /// <param name="value">TreatmentArm value.</param>
        /// <param name="settings">Correction settings.</param>
        /// <returns>True when the configured short-arm allowlist contains the value.</returns>
        private static bool isProtectedShortTreatmentArm(string? value, ClaudeApiCorrectionSettings settings)
        {
            #region implementation

            return !string.IsNullOrWhiteSpace(value)
                && settings.ProtectedShortTreatmentArms.Any(v =>
                    string.Equals(v, value.Trim(), StringComparison.OrdinalIgnoreCase));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether an existing TreatmentArm value is a known header or generic label echo.
        /// </summary>
        /// <param name="value">TreatmentArm value.</param>
        /// <param name="context">Source table correction context.</param>
        /// <returns>True when the value is structural rather than a real arm.</returns>
        private static bool isHeaderOrGenericTreatmentArm(string? value, ClaudeCorrectionContext context)
        {
            #region implementation

            var normalized = ClaudeCorrectionContext.NormalizeTextToken(value);
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
    }

    /**************************************************************/
    /// <summary>
    /// Rejects TreatmentArm proposals that are body-system or SOC labels.
    /// </summary>
    /// <seealso cref="ICorrectionGuardrail"/>
    internal sealed class TreatmentArmBodySystemGuardrail : ICorrectionGuardrail
    {
        #region Fields

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

        #endregion

        /**************************************************************/
        /// <inheritdoc/>
        public CorrectionGuardrailResult Validate(CorrectionGuardrailRequest request)
        {
            #region implementation

            if (!string.Equals(request.Field, "TreatmentArm", StringComparison.OrdinalIgnoreCase)
                || !request.Settings.RejectTreatmentArmBodySystem)
            {
                return CorrectionGuardrailResult.Accept();
            }

            var normalized = ClaudeCorrectionContext.NormalizeTextToken(request.ProposedValue);
            return !string.IsNullOrEmpty(normalized) && BodySystemTreatmentArmLabels.Contains(normalized)
                ? CorrectionGuardrailResult.Reject("TreatmentArmBodySystem")
                : CorrectionGuardrailResult.Accept();

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Rejects TreatmentArm proposals that exactly match source header tokens.
    /// </summary>
    /// <seealso cref="ICorrectionGuardrail"/>
    internal sealed class TreatmentArmHeaderTokenGuardrail : ICorrectionGuardrail
    {
        /**************************************************************/
        /// <inheritdoc/>
        public CorrectionGuardrailResult Validate(CorrectionGuardrailRequest request)
        {
            #region implementation

            if (!string.Equals(request.Field, "TreatmentArm", StringComparison.OrdinalIgnoreCase)
                || !request.Settings.RejectTreatmentArmHeaderToken)
            {
                return CorrectionGuardrailResult.Accept();
            }

            return request.SourceContext.IsHeaderToken(request.ProposedValue)
                ? CorrectionGuardrailResult.Reject("TreatmentArmHeaderToken")
                : CorrectionGuardrailResult.Accept();

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Rejects ParameterName proposals that strictly add tokens to the original name.
    /// </summary>
    /// <seealso cref="ICorrectionGuardrail"/>
    internal sealed class ParameterNameSupersetGuardrail : ICorrectionGuardrail
    {
        #region Fields

        /**************************************************************/
        /// <summary>
        /// Regex used for conservative clinical-token comparisons.
        /// </summary>
        private static readonly Regex WordTokenPattern = new(@"[A-Za-z0-9]+", RegexOptions.Compiled);

        #endregion

        /**************************************************************/
        /// <inheritdoc/>
        public CorrectionGuardrailResult Validate(CorrectionGuardrailRequest request)
        {
            #region implementation

            if (!string.Equals(request.Field, "ParameterName", StringComparison.OrdinalIgnoreCase)
                || !request.Settings.RejectParameterNameSuperset)
            {
                return CorrectionGuardrailResult.Accept();
            }

            return isStrictTokenSuperset(request.ProposedValue, request.Observation.ParameterName)
                ? CorrectionGuardrailResult.Reject("ParameterNameSuperset")
                : CorrectionGuardrailResult.Accept();

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
    }

    /**************************************************************/
    /// <summary>
    /// Rejects percent-column PrimaryValueType demotions from Percentage to Count.
    /// </summary>
    /// <seealso cref="ICorrectionGuardrail"/>
    internal sealed class PercentColumnTypeDemotionGuardrail : ICorrectionGuardrail
    {
        /**************************************************************/
        /// <inheritdoc/>
        public CorrectionGuardrailResult Validate(CorrectionGuardrailRequest request)
        {
            #region implementation

            if (!string.Equals(request.Field, "PrimaryValueType", StringComparison.OrdinalIgnoreCase)
                || !request.Settings.EnforcePercentColumnConsistency)
            {
                return CorrectionGuardrailResult.Accept();
            }

            return request.SourceContext.IsPercentColumn(request.Observation.SourceCellSeq)
                   && string.Equals(request.Observation.PrimaryValueType, "Percentage", StringComparison.OrdinalIgnoreCase)
                   && string.Equals(request.ProposedValue, "Count", StringComparison.OrdinalIgnoreCase)
                ? CorrectionGuardrailResult.Reject("PercentColumnTypeDemotion")
                : CorrectionGuardrailResult.Accept();

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Rejects percent-unit assignments to rows that remain text typed.
    /// </summary>
    /// <seealso cref="ICorrectionGuardrail"/>
    internal sealed class TextRowUnitPercentGuardrail : ICorrectionGuardrail
    {
        /**************************************************************/
        /// <inheritdoc/>
        public CorrectionGuardrailResult Validate(CorrectionGuardrailRequest request)
        {
            #region implementation

            if (!string.Equals(request.Field, "Unit", StringComparison.OrdinalIgnoreCase)
                || !request.Settings.RejectTextRowUnitPercent)
            {
                return CorrectionGuardrailResult.Accept();
            }

            return string.Equals(request.ProposedValue, "%", StringComparison.Ordinal)
                   && string.Equals(request.Observation.PrimaryValueType, "Text", StringComparison.OrdinalIgnoreCase)
                ? CorrectionGuardrailResult.Reject("TextRowUnitPercent")
                : CorrectionGuardrailResult.Accept();

            #endregion
        }
    }
}
