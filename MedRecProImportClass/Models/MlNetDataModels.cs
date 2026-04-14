using Microsoft.ML.Data;

namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// ML.NET input and prediction data classes for the Stage 3.4 ML correction pipeline.
    /// Each pair of Input/Prediction classes corresponds to one stage of the
    /// <see cref="MedRecProImportClass.Service.TransformationServices.MlNetCorrectionService"/>.
    /// </summary>
    /// <remarks>
    /// ## Class Pairs by Stage
    /// - **Stage 1**: <see cref="TableCategoryInput"/> / <see cref="TableCategoryPrediction"/>
    /// - **Stage 2**: <see cref="DoseRegimenRoutingInput"/> / <see cref="DoseRegimenRoutingPrediction"/>
    /// - **Stage 3**: <see cref="PrimaryValueTypeInput"/> / <see cref="PrimaryValueTypePrediction"/>
    /// - **Stage 4**: <see cref="AnomalyInput"/> / <see cref="AnomalyPrediction"/>
    ///
    /// All classes are declared <c>internal</c> — they are consumed only within
    /// <c>MedRecProImportClass</c> and are not part of the public API.
    /// </remarks>
    /// <seealso cref="MedRecProImportClass.Service.TransformationServices.MlNetCorrectionService"/>
    /// <seealso cref="MlNetCorrectionSettings"/>
    internal static class MlNetDataModels { }

    #region Stage 1 — TableCategory

    /**************************************************************/
    /// <summary>Stage 1 input: features for TableCategory classification.</summary>
    /// <seealso cref="TableCategoryPrediction"/>
    internal class TableCategoryInput
    {
        /**************************************************************/
        /// <summary>Table caption text.</summary>
        [LoadColumn(0)]
        public string Caption { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>Section title.</summary>
        [LoadColumn(1)]
        public string SectionTitle { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>Parent section LOINC code.</summary>
        [LoadColumn(2)]
        public string ParentSectionCode { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>Parse rule that matched.</summary>
        [LoadColumn(3)]
        public string ParseRule { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>Label: the table category (used for training, ignored during prediction).</summary>
        [LoadColumn(4)]
        public string TableCategory { get; set; } = string.Empty;
    }

    /**************************************************************/
    /// <summary>Stage 1 prediction output.</summary>
    /// <seealso cref="TableCategoryInput"/>
    internal class TableCategoryPrediction
    {
        /**************************************************************/
        /// <summary>Predicted table category.</summary>
        [ColumnName("PredictedLabel")]
        public string? PredictedLabel { get; set; }

        /**************************************************************/
        /// <summary>Per-class confidence scores.</summary>
        [ColumnName("Score")]
        public float[]? Score { get; set; }
    }

    #endregion Stage 1

    #region Stage 2 — DoseRegimen Routing

    /**************************************************************/
    /// <summary>Stage 2 input: features for DoseRegimen routing classification.</summary>
    /// <seealso cref="DoseRegimenRoutingPrediction"/>
    internal class DoseRegimenRoutingInput
    {
        /**************************************************************/
        /// <summary>DoseRegimen content to route.</summary>
        [LoadColumn(0)]
        public string DoseRegimen { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>Table category for context.</summary>
        [LoadColumn(1)]
        public string TableCategory { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>Table caption for context.</summary>
        [LoadColumn(2)]
        public string Caption { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>Parameter name for context.</summary>
        [LoadColumn(3)]
        public string ParameterName { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>Binary signal: 1f if DoseExtractor parsed a dose, 0f otherwise. Strong "Keep" discriminator.</summary>
        [LoadColumn(4)]
        public float HasDose { get; set; }

        /**************************************************************/
        /// <summary>Label: routing target (ParameterSubtype, Population, Timepoint, Keep).</summary>
        [LoadColumn(5)]
        public string? RoutingTarget { get; set; }
    }

    /**************************************************************/
    /// <summary>Stage 2 prediction output.</summary>
    /// <seealso cref="DoseRegimenRoutingInput"/>
    internal class DoseRegimenRoutingPrediction
    {
        /**************************************************************/
        /// <summary>Predicted routing target.</summary>
        [ColumnName("PredictedLabel")]
        public string? PredictedLabel { get; set; }

        /**************************************************************/
        /// <summary>Per-class confidence scores.</summary>
        [ColumnName("Score")]
        public float[]? Score { get; set; }
    }

    #endregion Stage 2

    #region Stage 3 — PrimaryValueType Disambiguation

    /**************************************************************/
    /// <summary>Stage 3 input: features for PrimaryValueType disambiguation.</summary>
    /// <seealso cref="PrimaryValueTypePrediction"/>
    internal class PrimaryValueTypeInput
    {
        /**************************************************************/
        /// <summary>Measurement unit.</summary>
        [LoadColumn(0)]
        public string Unit { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>Table category for context.</summary>
        [LoadColumn(1)]
        public string TableCategory { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>Parse rule that matched.</summary>
        [LoadColumn(2)]
        public string ParseRule { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>Table caption for context.</summary>
        [LoadColumn(3)]
        public string Caption { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>Whether LowerBound is present (1.0) or absent (0.0).</summary>
        [LoadColumn(4)]
        public float HasLowerBound { get; set; }

        /**************************************************************/
        /// <summary>Whether UpperBound is present (1.0) or absent (0.0).</summary>
        [LoadColumn(5)]
        public float HasUpperBound { get; set; }

        /**************************************************************/
        /// <summary>Label: the PrimaryValueType (used for training, ignored during prediction).</summary>
        [LoadColumn(6)]
        public string PrimaryValueType { get; set; } = string.Empty;
    }

    /**************************************************************/
    /// <summary>Stage 3 prediction output.</summary>
    /// <seealso cref="PrimaryValueTypeInput"/>
    internal class PrimaryValueTypePrediction
    {
        /**************************************************************/
        /// <summary>Predicted PrimaryValueType.</summary>
        [ColumnName("PredictedLabel")]
        public string? PredictedLabel { get; set; }

        /**************************************************************/
        /// <summary>Per-class confidence scores.</summary>
        [ColumnName("Score")]
        public float[]? Score { get; set; }
    }

    #endregion Stage 3

    #region Stage 4 — Anomaly Detection

    /**************************************************************/
    /// <summary>
    /// Stage 4 input: individual named columns for PCA anomaly detection.
    /// Each property maps to an ML.NET column. At training time, only columns with real
    /// variance are included in the model via a dynamic <c>Concatenate("Features", ...)</c>
    /// pipeline step — constant-zero columns are excluded entirely rather than jittered.
    /// At scoring time, the baked-in Concatenate reads only the active columns; values in
    /// unused properties are ignored.
    /// </summary>
    /// <remarks>
    /// Nulls from <see cref="ParsedObservation"/> are mapped to 0 via
    /// <see cref="MlTrainingRecord.toSafeFloat"/>. LogArmN is <c>log(ArmN + 1)</c>
    /// to compress the 5–8500+ range.
    /// </remarks>
    /// <seealso cref="AnomalyPrediction"/>
    internal class AnomalyFeatureRow
    {
        /**************************************************************/
        /// <summary>Primary observation value (e.g., incidence rate, mean concentration).</summary>
        public float PrimaryValue { get; set; }

        /**************************************************************/
        /// <summary>Secondary/variability measure (SD, CV, etc.). Zero when not parsed.</summary>
        public float SecondaryValue { get; set; }

        /**************************************************************/
        /// <summary>Confidence interval lower bound. Zero when not parsed.</summary>
        public float LowerBound { get; set; }

        /**************************************************************/
        /// <summary>Confidence interval upper bound. Zero when not parsed.</summary>
        public float UpperBound { get; set; }

        /**************************************************************/
        /// <summary>Statistical significance (p-value). Zero when not parsed.</summary>
        public float PValue { get; set; }

        /**************************************************************/
        /// <summary>Parser extraction confidence (0–1).</summary>
        public float ParseConfidence { get; set; }

        /**************************************************************/
        /// <summary>Log-transformed study arm participant count: <c>log(ArmN + 1)</c>.</summary>
        public float LogArmN { get; set; }
    }

    /**************************************************************/
    /// <summary>Stage 4 anomaly detection output.</summary>
    /// <seealso cref="AnomalyFeatureRow"/>
    internal class AnomalyPrediction
    {
        /**************************************************************/
        /// <summary>Whether the observation is flagged as an anomaly.</summary>
        [ColumnName("PredictedLabel")]
        public bool IsAnomaly { get; set; }

        /**************************************************************/
        /// <summary>Anomaly score — higher values indicate more anomalous observations.</summary>
        [ColumnName("Score")]
        public float Score { get; set; }
    }

    #endregion Stage 4
}
