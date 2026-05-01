using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Deterministic parse-quality evaluator. Computes a per-observation quality score in
    /// [0.0, 1.0] from structural parse-failure signals — null PrimaryValue / PrimaryValueType,
    /// missing Required columns for the observation's TableCategory, garbage content in Unit
    /// or ParameterSubtype, soft repair flags from upstream parsers, and the parser's own
    /// self-reported confidence. The score drives downstream Claude forwarding: rows whose
    /// score falls below <see cref="ClaudeApiCorrectionSettings.ClaudeReviewQualityThreshold"/>
    /// are sent to the Claude API for AI correction.
    /// </summary>
    /// <remarks>
    /// ## Why This Exists
    /// Replaces the retired Stage 4 PCA anomaly pipeline. PCA reconstruction-error scores
    /// clustered in a narrow band regardless of training-set shape, making any fixed threshold
    /// a continual tuning exercise. This service is rule-based and targets parse-alignment
    /// failures directly — scoring the probability that a row assembled cleanly rather than
    /// whether its values are "unusual".
    ///
    /// ## Output Shape
    /// Returns <see cref="ParseQualityScore"/> containing:
    /// - <c>Score</c>: float in [0.0, 1.0]. 1.0 = no penalties detected. Lower values mean
    ///   more severe / more numerous parse failures.
    /// - <c>Reasons</c>: pipe-delimitable list of human-readable rule names that fired.
    ///   Empty when Score == 1.0.
    ///
    /// The caller (<see cref="QCNetCorrectionService.ScoreAndCorrect"/>) emits both as
    /// <c>QC_PARSE_QUALITY:{Score:F4}</c> and
    /// <c>QC_PARSE_QUALITY:REVIEW_REASONS:{pipe-joined Reasons}</c> ValidationFlags.
    /// </remarks>
    /// <seealso cref="IColumnContractRegistry"/>
    /// <seealso cref="QCNetCorrectionService"/>
    /// <seealso cref="ClaudeApiCorrectionService"/>
    public interface IParseQualityService
    {
        /**************************************************************/
        /// <summary>
        /// Evaluates a single observation against the parse-quality rule set.
        /// </summary>
        /// <param name="obs">Observation to evaluate. Read-only — the service does not mutate it.</param>
        /// <returns>A <see cref="ParseQualityScore"/> with score and triggered reason list.</returns>
        ParseQualityScore Evaluate(ParsedObservation obs);
    }

    /**************************************************************/
    /// <summary>
    /// Result of <see cref="IParseQualityService.Evaluate"/>. Immutable record carrying the
    /// numeric quality score and the list of rule names whose penalties fired.
    /// </summary>
    /// <param name="Score">Quality score in [0.0, 1.0]. 1.0 = clean; lower = more penalties.</param>
    /// <param name="Reasons">Rule names that fired. Empty when <paramref name="Score"/> is 1.0.</param>
    public sealed record ParseQualityScore(float Score, List<string> Reasons);
}
