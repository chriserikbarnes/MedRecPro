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
    ///
    /// Stage 4 (anomaly scoring) was retired on 2026-04-24 in favor of the deterministic
    /// parse-quality gate implemented by
    /// <see cref="MedRecProImportClass.Service.TransformationServices.IParseQualityService"/>.
    /// Anomaly scores were a poor proxy for parse-alignment errors and clustered in a narrow
    /// band regardless of training-set shape.
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
}
