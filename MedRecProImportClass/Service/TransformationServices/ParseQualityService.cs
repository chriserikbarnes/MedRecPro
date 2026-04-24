using MedRecProImportClass.Models;
using System.Text.RegularExpressions;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Deterministic <see cref="IParseQualityService"/> implementation. Starts from a perfect
    /// score of 1.0 and applies a sequence of multiplicative penalties based on observable
    /// parse-failure signals — null / text PrimaryValue, per-category Required-column misses,
    /// structural garbage in Unit / ParameterSubtype, soft upstream-repair flags, and a
    /// floor at <see cref="ParsedObservation.ParseConfidence"/>.
    /// </summary>
    /// <remarks>
    /// ## Penalty Formula (spec)
    ///
    /// ### Hard failures (critical)
    /// Drive score toward zero quickly; target rows that cannot be analyzed at all.
    /// <code>
    /// × 0.2 if PrimaryValue IS NULL
    /// × 0.2 if PrimaryValueType IS NULL
    /// × 0.3 if PrimaryValueType = "Text"
    /// × 0.3 if ParameterName IS NULL  (honors per-category contract — only when Required)
    /// × 0.4 if TableCategory IS NULL
    /// </code>
    ///
    /// ### Per-category Required misses
    /// Foreach column marked Required by <see cref="IColumnContractRegistry"/> for this
    /// observation's <see cref="ParsedObservation.TableCategory"/>:
    /// <code>× 0.6  if obs.&lt;col&gt; IS NULL or empty</code>
    ///
    /// ### Structural garbage
    /// Field populated but with the wrong kind of content.
    /// <code>
    /// × 0.5 if Unit matches BadUnitPattern (contains digits except micro-prefix,
    ///                                       length &gt; 25 chars,
    ///                                       age-range / week-range / n=… / "years" / "Weeks")
    /// × 0.5 if ParameterSubtype matches BadSubtypePattern (statistical-format labels,
    ///                                                      food-status, frequencies,
    ///                                                      dose phrases with units)
    /// × 0.7 if LowerBound &lt; 0 AND PrimaryValueType ∈ {ArithmeticMean, GeometricMean,
    ///                                                   Percentage, Count}
    /// </code>
    ///
    /// ### Soft repair signals
    /// Parser succeeded but had to work for it. Modest penalty.
    /// <code>
    /// × 0.9  if ValidationFlags contains "PVT_MIGRATED"
    /// × 0.9  if ValidationFlags contains "BOUND_TYPE_INFERRED"
    /// × 0.9  if ValidationFlags contains "CAPTION_REINTERPRET"
    /// × 0.9  if ValidationFlags contains "PLUSMINUS_TYPE_INFERRED"
    /// × 0.95 if ValidationFlags contains "PK_UNIT_SIBLING_VOTED" without ":RESCUE_BOOST"
    /// × 0.85 if ValidationFlags contains "PK_UNIT_SIBLING_VOTED:RESCUE_BOOST"
    /// × 0.9  if ValidationFlags contains "COL_STD:PK_NAME_PARKED_CTX"
    /// × 0.9  if ValidationFlags contains "MISSING_R_Unit"
    /// </code>
    ///
    /// ### ParseConfidence floor
    /// <code>score = min(score, ParseConfidence ?? 1.0)</code>
    /// Prevents low-confidence rows from skipping review even with no specific flag hits.
    ///
    /// ## Reason Strings
    /// Every multiplier that fires pushes a short stable token into <c>Reasons</c>:
    /// <c>PrimaryValueNull</c>, <c>PrimaryValueTypeNull</c>, <c>PrimaryValueTypeText</c>,
    /// <c>ParameterNameNull</c>, <c>TableCategoryNull</c>,
    /// <c>MissingRequired:{ColumnName}</c>, <c>BadUnit</c>, <c>BadSubtype</c>,
    /// <c>NegativeBoundOnNonNegativeType</c>, <c>SoftRepair:PVT_MIGRATED</c>, etc.
    /// The caller concatenates them pipe-delimited into
    /// <c>QC_PARSE_QUALITY:REVIEW_REASONS:{list}</c>.
    /// </remarks>
    /// <seealso cref="IParseQualityService"/>
    /// <seealso cref="IColumnContractRegistry"/>
    public class ParseQualityService : IParseQualityService
    {
        #region Fields

        /**************************************************************/
        /// <summary>Per-category column contracts keyed by TableCategory.</summary>
        private readonly IColumnContractRegistry _contracts;

        #endregion

        #region Regex patterns

        /**************************************************************/
        /// <summary>
        /// Matches Unit field content that is clearly not a unit: contains digits (other
        /// than a leading micro-prefix µ/mc/pp-style rollover), exceeds 25 chars (canonical
        /// units are short — even <c>mcg·h/mL</c> is 8 chars), or matches known caption-leak
        /// patterns like age ranges, week ranges, sample-size embedding, or the literal words
        /// "years" / "Weeks".
        /// </summary>
        /// <remarks>
        /// <c>µ</c>, <c>mc</c>, <c>percentage points</c> never contain digits themselves, so
        /// any digit in the string is strong signal that real unit content has been polluted
        /// by a caption echo (e.g. <c>"Ages 27-58 yrs"</c>, <c>"6 to 12 Weeks) (n = 11"</c>).
        /// </remarks>
        private static readonly Regex _badUnitPattern = new(
            @"(\d)|^.{26,}$|age\s*range|ages?\s+\d|weeks?\b|years?\b|n\s*=\s*\d",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Matches ParameterSubtype field content that is clearly not a subtype qualifier:
        /// statistical-format labels, food-status words, dosing frequencies, or dose phrases
        /// with units. These are the six visible leak categories surfaced by the 2026-04-23
        /// Stage 2 audit.
        /// </summary>
        private static readonly Regex _badSubtypePattern = new(
            @"(?:mean\s*[±\(]|median\s*[±\(]|\bcv\b\s*\(%?\)|mean\s*±\s*standard\s*deviation|"
            + @"median\s*\(\s*range\s*\)|\bfed\b|\bfasted\b|every\s+\d+\s*h(?:our)?s?|q\d+h|"
            + @"\b(?:once|twice)\s+daily|\bdaily\b|^\s*-?\d+\.?\d*\s*(?:mg|mcg|µg|g|ml|units?)\b|"
            + @"\bsingle\s+\d+\s*(?:mg|g|ml|mcg)\s+dose)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes the service with a column-contract registry. The registry drives the
        /// per-category Required-miss penalty — without it the formula would have to hardcode
        /// contracts, duplicating <c>column-contracts.md</c>.
        /// </summary>
        /// <param name="contracts">Per-TableCategory column-contract lookup.</param>
        public ParseQualityService(IColumnContractRegistry contracts)
        {
            #region implementation

            _contracts = contracts;

            #endregion
        }

        #endregion

        #region IParseQualityService Implementation

        /**************************************************************/
        /// <inheritdoc/>
        public ParseQualityScore Evaluate(ParsedObservation obs)
        {
            #region implementation

            double score = 1.0;
            var reasons = new List<string>();

            // Hard failures — these are the user's explicitly-flagged "super bad" signals.
            if (!obs.PrimaryValue.HasValue)
            {
                score *= 0.2;
                reasons.Add("PrimaryValueNull");
            }

            if (string.IsNullOrWhiteSpace(obs.PrimaryValueType))
            {
                score *= 0.2;
                reasons.Add("PrimaryValueTypeNull");
            }
            else if (string.Equals(obs.PrimaryValueType, "Text", StringComparison.OrdinalIgnoreCase))
            {
                score *= 0.3;
                reasons.Add("PrimaryValueTypeText");
            }

            if (string.IsNullOrWhiteSpace(obs.TableCategory))
            {
                score *= 0.4;
                reasons.Add("TableCategoryNull");
            }

            // ParameterName null is "super bad" only for categories that Require it. We use the
            // same per-category contract lookup both for the ParameterName check and for the
            // generic Required-miss loop below.
            var contract = _contracts.GetContract(obs.TableCategory);
            var nameRequired = contract.Required.Contains("ParameterName");

            if (nameRequired && string.IsNullOrWhiteSpace(obs.ParameterName))
            {
                score *= 0.3;
                reasons.Add("ParameterNameNull");
            }

            // Per-category Required misses. ParameterName and PrimaryValue / PrimaryValueType
            // are already penalized above under the hard-failure banner; skip them here to
            // avoid double-counting.
            foreach (var col in contract.Required)
            {
                if (isAlreadyPenalizedHardFailure(col))
                    continue;
                if (!isColumnEmpty(obs, col))
                    continue;

                score *= 0.6;
                reasons.Add($"MissingRequired:{col}");
            }

            // Structural garbage — field present, wrong content
            if (!string.IsNullOrWhiteSpace(obs.Unit) && _badUnitPattern.IsMatch(obs.Unit))
            {
                score *= 0.5;
                reasons.Add("BadUnit");
            }

            if (!string.IsNullOrWhiteSpace(obs.ParameterSubtype) && _badSubtypePattern.IsMatch(obs.ParameterSubtype))
            {
                score *= 0.5;
                reasons.Add("BadSubtype");
            }

            if (obs.LowerBound.HasValue && obs.LowerBound.Value < 0 && isNonNegativePvt(obs.PrimaryValueType))
            {
                score *= 0.7;
                reasons.Add("NegativeBoundOnNonNegativeType");
            }

            // Soft repair signals — upstream parser had to work for this row.
            var flags = obs.ValidationFlags ?? string.Empty;
            if (flags.Contains("PVT_MIGRATED", StringComparison.OrdinalIgnoreCase))
            {
                score *= 0.9;
                reasons.Add("SoftRepair:PVT_MIGRATED");
            }
            if (flags.Contains("BOUND_TYPE_INFERRED", StringComparison.OrdinalIgnoreCase))
            {
                score *= 0.9;
                reasons.Add("SoftRepair:BOUND_TYPE_INFERRED");
            }
            if (flags.Contains("CAPTION_REINTERPRET", StringComparison.OrdinalIgnoreCase))
            {
                score *= 0.9;
                reasons.Add("SoftRepair:CAPTION_REINTERPRET");
            }
            if (flags.Contains("PLUSMINUS_TYPE_INFERRED", StringComparison.OrdinalIgnoreCase))
            {
                score *= 0.9;
                reasons.Add("SoftRepair:PLUSMINUS_TYPE_INFERRED");
            }
            if (flags.Contains("PK_UNIT_SIBLING_VOTED:RESCUE_BOOST", StringComparison.OrdinalIgnoreCase))
            {
                // Rescue-boost subsumes the plain sibling-voted penalty. Apply the stricter
                // 0.85 multiplier and skip the 0.95 check.
                score *= 0.85;
                reasons.Add("SoftRepair:PK_UNIT_SIBLING_VOTED:RESCUE_BOOST");
            }
            else if (flags.Contains("PK_UNIT_SIBLING_VOTED", StringComparison.OrdinalIgnoreCase))
            {
                score *= 0.95;
                reasons.Add("SoftRepair:PK_UNIT_SIBLING_VOTED");
            }
            if (flags.Contains("COL_STD:PK_NAME_PARKED_CTX", StringComparison.OrdinalIgnoreCase))
            {
                score *= 0.9;
                reasons.Add("SoftRepair:PK_NAME_PARKED_CTX");
            }
            if (flags.Contains("MISSING_R_Unit", StringComparison.OrdinalIgnoreCase))
            {
                score *= 0.9;
                reasons.Add("SoftRepair:MISSING_R_Unit");
            }

            // ParseConfidence floor — a row with no specific flag hits but low parser
            // self-confidence still deserves review.
            if (obs.ParseConfidence.HasValue)
            {
                var floor = (double)obs.ParseConfidence.Value;
                if (floor < score)
                {
                    score = floor;
                    reasons.Add("ParseConfidenceFloor");
                }
            }

            // Clamp to [0, 1] defensively.
            score = Math.Max(0.0, Math.Min(1.0, score));

            return new ParseQualityScore((float)score, reasons);

            #endregion
        }

        #endregion

        #region Helpers

        /**************************************************************/
        /// <summary>
        /// Returns true when a Required-column key refers to a field that has already been
        /// penalized under the hard-failure banner. Prevents double-counting.
        /// </summary>
        /// <param name="col">Required-column name.</param>
        private static bool isAlreadyPenalizedHardFailure(string col)
        {
            #region implementation

            return string.Equals(col, "PrimaryValue", StringComparison.OrdinalIgnoreCase)
                || string.Equals(col, "PrimaryValueType", StringComparison.OrdinalIgnoreCase)
                || string.Equals(col, "ParameterName", StringComparison.OrdinalIgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true when the observation's value for the given column name is null,
        /// empty, or whitespace. Handles the string, numeric, and DateTime field types on
        /// <see cref="ParsedObservation"/> that appear in the per-category contracts.
        /// </summary>
        /// <remarks>
        /// Any column name not covered by the switch silently returns false (treated as
        /// populated). The Required-set in <see cref="ColumnContractRegistry"/> is the
        /// closed universe of column names that can reach this method; adding a new
        /// Required entry there requires adding a case here.
        /// </remarks>
        /// <param name="obs">Observation being evaluated.</param>
        /// <param name="col">Column name from <see cref="CategoryContract.Required"/>.</param>
        /// <returns>True if the column is null / empty for this observation.</returns>
        private static bool isColumnEmpty(ParsedObservation obs, string col)
        {
            #region implementation

            return col.ToLowerInvariant() switch
            {
                "parametername" => string.IsNullOrWhiteSpace(obs.ParameterName),
                "parametercategory" => string.IsNullOrWhiteSpace(obs.ParameterCategory),
                "parametersubtype" => string.IsNullOrWhiteSpace(obs.ParameterSubtype),
                "treatmentarm" => string.IsNullOrWhiteSpace(obs.TreatmentArm),
                "armn" => !obs.ArmN.HasValue,
                "studycontext" => string.IsNullOrWhiteSpace(obs.StudyContext),
                "doseregimen" => string.IsNullOrWhiteSpace(obs.DoseRegimen),
                "dose" => !obs.Dose.HasValue,
                "doseunit" => string.IsNullOrWhiteSpace(obs.DoseUnit),
                "population" => string.IsNullOrWhiteSpace(obs.Population),
                "timepoint" => string.IsNullOrWhiteSpace(obs.Timepoint),
                "time" => !obs.Time.HasValue,
                "timeunit" => string.IsNullOrWhiteSpace(obs.TimeUnit),
                "rawvalue" => string.IsNullOrWhiteSpace(obs.RawValue),
                "primaryvalue" => !obs.PrimaryValue.HasValue,
                "primaryvaluetype" => string.IsNullOrWhiteSpace(obs.PrimaryValueType),
                "secondaryvalue" => !obs.SecondaryValue.HasValue,
                "secondaryvaluetype" => string.IsNullOrWhiteSpace(obs.SecondaryValueType),
                "lowerbound" => !obs.LowerBound.HasValue,
                "upperbound" => !obs.UpperBound.HasValue,
                "boundtype" => string.IsNullOrWhiteSpace(obs.BoundType),
                "pvalue" => !obs.PValue.HasValue,
                "unit" => string.IsNullOrWhiteSpace(obs.Unit),
                _ => false,
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true when the PrimaryValueType describes a statistic whose value is
        /// physically nonnegative (means of nonnegative quantities, percentages, counts).
        /// Used to gate the negative-LowerBound penalty — a negative 95CI lower bound on a
        /// <c>HazardRatio</c> is fine, but on an <c>ArithmeticMean</c> of a concentration
        /// it signals arithmetic error.
        /// </summary>
        /// <param name="pvt">PrimaryValueType string.</param>
        private static bool isNonNegativePvt(string? pvt)
        {
            #region implementation

            if (string.IsNullOrEmpty(pvt)) return false;
            return string.Equals(pvt, "ArithmeticMean", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pvt, "GeometricMean", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pvt, "Percentage", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pvt, "Count", StringComparison.OrdinalIgnoreCase);

            #endregion
        }

        #endregion
    }
}
