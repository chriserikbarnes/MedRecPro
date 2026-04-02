namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Compact 19-field serializable DTO containing only the fields consumed by the 4 ML training
    /// methods in <see cref="MedRecProImportClass.Service.TransformationServices.MlNetCorrectionService"/>.
    /// Omits all provenance, raw value, and audit fields from <see cref="ParsedObservation"/> to
    /// minimize serialized size (~100 bytes per record with <c>WhenWritingNull</c>).
    /// </summary>
    /// <remarks>
    /// ## Field Mapping by Training Stage
    /// - **Stage 1 (TableCategory)**: Caption, SectionTitle, ParentSectionCode, ParseRule, TableCategory
    /// - **Stage 2 (DoseRegimen Routing)**: DoseRegimen, TableCategory, Caption, ParameterName, ValidationFlags
    /// - **Stage 3 (PrimaryValueType)**: Unit, TableCategory, ParseRule, Caption, HasLowerBound, HasUpperBound, PrimaryValueType
    /// - **Stage 4 (Anomaly Detection)**: PrimaryValue, SecondaryValue, LowerBound, UpperBound, PValue, ParseConfidence
    ///
    /// ## Ground Truth vs Bootstrap
    /// Records with <see cref="IsClaudeGroundTruth"/> = true originate from Claude-corrected observations
    /// and are prioritized during eviction (bootstrap records are evicted first).
    ///
    /// ## Size Estimate
    /// At 100K records ≈ 10 MB on disk with <c>System.Text.Json</c> and <c>WhenWritingNull</c>.
    /// </remarks>
    /// <seealso cref="MlTrainingStoreState"/>
    /// <seealso cref="MedRecProImportClass.Service.TransformationServices.IMlTrainingStore"/>
    public class MlTrainingRecord
    {
        #region Stage 1 features

        /**************************************************************/
        /// <summary>Table category label — Stage 1 training target and Stage 2/3 feature.</summary>
        public string? TableCategory { get; set; }

        /**************************************************************/
        /// <summary>Table caption text, truncated to 200 chars on creation.</summary>
        public string? Caption { get; set; }

        /**************************************************************/
        /// <summary>Section title text, truncated to 200 chars on creation.</summary>
        public string? SectionTitle { get; set; }

        /**************************************************************/
        /// <summary>Parent section LOINC code.</summary>
        public string? ParentSectionCode { get; set; }

        /**************************************************************/
        /// <summary>Parse rule that matched the observation.</summary>
        public string? ParseRule { get; set; }

        #endregion Stage 1 features

        #region Stage 2 features + label synthesis

        /**************************************************************/
        /// <summary>DoseRegimen content — Stage 2 routing input and label synthesis source.</summary>
        public string? DoseRegimen { get; set; }

        /**************************************************************/
        /// <summary>Parameter name — Stage 2 routing context feature.</summary>
        public string? ParameterName { get; set; }

        /**************************************************************/
        /// <summary>Validation flags — used for COL_STD:* label synthesis in Stage 2.</summary>
        public string? ValidationFlags { get; set; }

        #endregion Stage 2 features + label synthesis

        #region Stage 3 features + label

        /**************************************************************/
        /// <summary>Measurement unit — Stage 3 disambiguation feature.</summary>
        public string? Unit { get; set; }

        /**************************************************************/
        /// <summary>Whether the observation has a lower bound value.</summary>
        public bool HasLowerBound { get; set; }

        /**************************************************************/
        /// <summary>Whether the observation has an upper bound value.</summary>
        public bool HasUpperBound { get; set; }

        /**************************************************************/
        /// <summary>PrimaryValueType — Stage 3 training label.</summary>
        public string? PrimaryValueType { get; set; }

        #endregion Stage 3 features + label

        #region Stage 4 anomaly features (6-slot PCA vector)

        /**************************************************************/
        /// <summary>Primary numeric value (cast from double? to float).</summary>
        public float PrimaryValue { get; set; }

        /**************************************************************/
        /// <summary>Secondary numeric value, e.g. SD or SE (cast from double? to float).</summary>
        public float SecondaryValue { get; set; }

        /**************************************************************/
        /// <summary>Lower bound of confidence interval (cast from double? to float).</summary>
        public float LowerBound { get; set; }

        /**************************************************************/
        /// <summary>Upper bound of confidence interval (cast from double? to float).</summary>
        public float UpperBound { get; set; }

        /**************************************************************/
        /// <summary>P-value (cast from double? to float).</summary>
        public float PValue { get; set; }

        /**************************************************************/
        /// <summary>Parse confidence score (cast from double? to float).</summary>
        public float ParseConfidence { get; set; }

        #endregion Stage 4 anomaly features

        #region Metadata

        /**************************************************************/
        /// <summary>
        /// True if this record originates from a Claude-corrected observation (ground truth).
        /// False if it was bootstrapped from high-confidence parse results.
        /// Ground-truth records are prioritized during eviction (bootstrap records evicted first).
        /// </summary>
        public bool IsClaudeGroundTruth { get; set; }

        #endregion Metadata

        #region Factory Method

        /**************************************************************/
        /// <summary>
        /// Creates an <see cref="MlTrainingRecord"/> from a <see cref="ParsedObservation"/>,
        /// truncating Caption and SectionTitle to 200 chars and casting double? fields to float.
        /// </summary>
        /// <param name="obs">Source observation.</param>
        /// <param name="isGroundTruth">
        /// True if the observation was corrected by Claude (ground truth).
        /// False for high-confidence bootstrap records.
        /// </param>
        /// <returns>A compact training record suitable for persistence and ML training.</returns>
        /// <example>
        /// <code>
        /// var record = MlTrainingRecord.FromObservation(obs, isGroundTruth: true);
        /// </code>
        /// </example>
        public static MlTrainingRecord FromObservation(ParsedObservation obs, bool isGroundTruth)
        {
            #region implementation

            return new MlTrainingRecord
            {
                // Stage 1 features
                TableCategory = obs.TableCategory,
                Caption = truncate(obs.Caption, 200),
                SectionTitle = truncate(obs.SectionTitle, 200),
                ParentSectionCode = obs.ParentSectionCode,
                ParseRule = obs.ParseRule,

                // Stage 2 features
                DoseRegimen = obs.DoseRegimen,
                ParameterName = obs.ParameterName,
                ValidationFlags = obs.ValidationFlags,

                // Stage 3 features
                Unit = obs.Unit,
                HasLowerBound = obs.LowerBound.HasValue,
                HasUpperBound = obs.UpperBound.HasValue,
                PrimaryValueType = obs.PrimaryValueType,

                // Stage 4 anomaly features (double? → float, NaN/Infinity clamped to 0f)
                PrimaryValue    = toSafeFloat(obs.PrimaryValue),
                SecondaryValue  = toSafeFloat(obs.SecondaryValue),
                LowerBound      = toSafeFloat(obs.LowerBound),
                UpperBound      = toSafeFloat(obs.UpperBound),
                PValue          = toSafeFloat(obs.PValue),
                ParseConfidence = toSafeFloat(obs.ParseConfidence),

                // Metadata
                IsClaudeGroundTruth = isGroundTruth
            };

            #endregion
        }

        #endregion Factory Method

        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Truncates a string to the specified maximum length.
        /// Returns null if the input is null.
        /// </summary>
        /// <param name="value">String to truncate.</param>
        /// <param name="maxLength">Maximum allowed length.</param>
        /// <returns>Truncated string or null.</returns>
        private static string? truncate(string? value, int maxLength)
        {
            #region implementation

            if (value == null)
                return null;
            return value.Length <= maxLength ? value : value[..maxLength];

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts a nullable double to a safe float, replacing null, NaN, and Infinity with 0f.
        /// </summary>
        /// <remarks>
        /// The pattern <c>(float)(value ?? 0.0)</c> guards against null but not <c>double.NaN</c>,
        /// which is non-null and casts directly to <c>float.NaN</c>. NaN in ML.NET feature vectors
        /// causes PCA eigenvector computation to produce NaN, triggering an
        /// <see cref="ArgumentOutOfRangeException"/> at training time.
        /// </remarks>
        /// <param name="value">Nullable double to convert.</param>
        /// <returns>Safe float value (0f when input is null, NaN, or Infinity).</returns>
        /// <seealso cref="FromObservation"/>
        internal static float toSafeFloat(double? value)
        {
            #region implementation

            return value is { } v && !double.IsNaN(v) && !double.IsInfinity(v) ? (float)v : 0f;

            #endregion
        }

        #endregion Helper Methods
    }
}
